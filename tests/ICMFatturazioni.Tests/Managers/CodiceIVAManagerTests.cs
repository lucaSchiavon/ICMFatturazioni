using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test del manager Codici IVA. Obiettivi:
///   1) validazione dei campi obbligatori + regola condizionale Natura ⟺ Aliquota=0;
///   2) unicità del Codice tra gli attivi;
///   3) delega corretta al repository + normalizzazione (trim, Natura azzerata);
///   4) DELETE con pattern doppia difesa (dipendenze);
///   5) tracciamento audit di create/delete.
/// Niente DB reale: si usa <see cref="FakeCodiceIVARepository"/>.
/// </summary>
public class CodiceIVAManagerTests
{
    // Codice imponibile valido di default (aliquota > 0, niente Natura).
    private static CodiceIVA Imponibile(string codice = "22", string descr = "IVA 22%", decimal aliquota = 22m) => new()
    {
        Codice      = codice,
        Descrizione = descr,
        Aliquota    = aliquota,
        Natura      = null,
    };

    // Codice esente valido (aliquota 0 + Natura obbligatoria + bollo scelto:
    // per le esenti l'Obbligo bollo è obbligatorio, Sì o No).
    private static CodiceIVA Esente(string codice = "N3", string descr = "Non imponibile", string natura = "N3") => new()
    {
        Codice       = codice,
        Descrizione  = descr,
        Aliquota     = 0m,
        Natura       = natura,
        ObbligoBollo = false,
    };

    private static CodiceIVAManager NewSut(FakeCodiceIVARepository fake, FakeAuditManager? audit = null)
        => new(fake, audit ?? new FakeAuditManager());

    // =================================================================
    // Audit
    // =================================================================

    [Fact]
    public async Task CreaAsync_RegistraAuditDiCreazione()
    {
        var fake = new FakeCodiceIVARepository();
        var audit = new FakeAuditManager();
        var sut = NewSut(fake, audit);

        var id = await sut.CreaAsync(Imponibile());

        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Creazione, voce.Operazione);
        Assert.Equal("CodiceIVA", voce.EntityType);
        Assert.Equal(id, voce.EntityId);
        Assert.Equal("22", voce.Descrizione);
    }

    [Fact]
    public async Task EliminaAsync_RegistraAuditDiEliminazione()
    {
        var fake = new FakeCodiceIVARepository();
        var audit = new FakeAuditManager();
        var sut = NewSut(fake, audit);
        var id = await sut.CreaAsync(Imponibile());
        audit.Voci.Clear();

        await sut.EliminaAsync(id);

        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Eliminazione, voce.Operazione);
        Assert.Equal(id, voce.EntityId);
    }

    // =================================================================
    // Validazione campi obbligatori
    // =================================================================

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreaAsync_CodiceVuoto_LanciaConMotivoCodiceObbligatorio(string codice)
    {
        var sut = NewSut(new FakeCodiceIVARepository());
        var input = Imponibile(codice: codice);

        var ex = await Assert.ThrowsAsync<CodiceIVAInvalidaException>(() => sut.CreaAsync(input));
        Assert.Equal(CodiceIVAInvalidaMotivo.CodiceObbligatorio, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_DescrizioneVuota_LanciaConMotivoDescrizioneObbligatoria()
    {
        var sut = NewSut(new FakeCodiceIVARepository());
        var input = Imponibile(descr: "  ");

        var ex = await Assert.ThrowsAsync<CodiceIVAInvalidaException>(() => sut.CreaAsync(input));
        Assert.Equal(CodiceIVAInvalidaMotivo.DescrizioneObbligatoria, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_AliquotaNegativa_LanciaConMotivoAliquotaNonValida()
    {
        var sut = NewSut(new FakeCodiceIVARepository());
        var input = Imponibile(aliquota: -1m);

        var ex = await Assert.ThrowsAsync<CodiceIVAInvalidaException>(() => sut.CreaAsync(input));
        Assert.Equal(CodiceIVAInvalidaMotivo.AliquotaNonValida, ex.Motivo);
    }

    // =================================================================
    // Regola condizionale Natura ⟺ Aliquota = 0
    // =================================================================

    [Fact]
    public async Task CreaAsync_Aliquota0SenzaNatura_LanciaNaturaObbligatoria()
    {
        var sut = NewSut(new FakeCodiceIVARepository());
        var input = new CodiceIVA { Codice = "ES", Descrizione = "Esente", Aliquota = 0m, Natura = null };

        var ex = await Assert.ThrowsAsync<CodiceIVAInvalidaException>(() => sut.CreaAsync(input));
        Assert.Equal(CodiceIVAInvalidaMotivo.NaturaObbligatoria, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_AliquotaImponibileConNatura_LanciaNaturaNonAmmessa()
    {
        var sut = NewSut(new FakeCodiceIVARepository());
        var input = new CodiceIVA { Codice = "22", Descrizione = "IVA 22%", Aliquota = 22m, Natura = "N3" };

        var ex = await Assert.ThrowsAsync<CodiceIVAInvalidaException>(() => sut.CreaAsync(input));
        Assert.Equal(CodiceIVAInvalidaMotivo.NaturaNonAmmessa, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_EsenteConNatura_Persiste()
    {
        var fake = new FakeCodiceIVARepository();
        var sut = NewSut(fake);

        var id = await sut.CreaAsync(Esente());

        var persistito = await fake.GetByIdAsync(id);
        Assert.NotNull(persistito);
        Assert.Equal(0m, persistito!.Aliquota);
        Assert.Equal("N3", persistito.Natura);
    }

    // =================================================================
    // Unicità del codice tra gli attivi
    // =================================================================

    [Fact]
    public async Task CreaAsync_CodiceDuplicatoTraAttivi_LanciaCodiceDuplicato()
    {
        var fake = new FakeCodiceIVARepository();
        var sut = NewSut(fake);
        await sut.CreaAsync(Imponibile(codice: "22"));

        // Confronto case-insensitive: "22" già esiste.
        var ex = await Assert.ThrowsAsync<CodiceIVAInvalidaException>(
            () => sut.CreaAsync(Imponibile(codice: "22", descr: "Doppione")));
        Assert.Equal(CodiceIVAInvalidaMotivo.CodiceDuplicato, ex.Motivo);
    }

    [Fact]
    public async Task AggiornaAsync_StessoCodiceSuStessoId_NonConsideraDuplicato()
    {
        var fake = new FakeCodiceIVARepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Imponibile(codice: "22", descr: "IVA 22%"));

        var modificato = new CodiceIVA
        {
            IdCodiceIVA = id,
            Codice      = "22",          // invariato
            Descrizione = "IVA ventidue", // cambiata
            Aliquota    = 22m,
            Natura      = null,
        };

        // Non deve lanciare: l'unicità esclude l'id corrente.
        await sut.AggiornaAsync(modificato);
        var persistito = await fake.GetByIdAsync(id);
        Assert.Equal("IVA ventidue", persistito!.Descrizione);
    }

    // =================================================================
    // Happy path + normalizzazione
    // =================================================================

    [Fact]
    public async Task CreaAsync_Valido_RestituisceIdEPersiste()
    {
        var fake = new FakeCodiceIVARepository();
        var sut = NewSut(fake);

        var id = await sut.CreaAsync(Imponibile());

        Assert.NotEqual(Guid.Empty, id);
        Assert.NotNull(await fake.GetByIdAsync(id));
    }

    [Fact]
    public async Task CreaAsync_NormalizzaCodiceEDescrizione_TrimEspazi()
    {
        var fake = new FakeCodiceIVARepository();
        var sut = NewSut(fake);

        var id = await sut.CreaAsync(new CodiceIVA
        {
            Codice = "  22  ", Descrizione = "  IVA 22%  ", Aliquota = 22m, Natura = null,
        });

        var persistito = await fake.GetByIdAsync(id);
        Assert.Equal("22", persistito!.Codice);
        Assert.Equal("IVA 22%", persistito.Descrizione);
    }

    [Fact]
    public async Task CreaAsync_NaturaWhitespaceConImponibile_VienePersistitaNull()
    {
        var fake = new FakeCodiceIVARepository();
        var sut = NewSut(fake);

        // Natura whitespace + aliquota imponibile: la normalizzazione la azzera,
        // così la regola condizionale è rispettata e non scatta NaturaNonAmmessa.
        var id = await sut.CreaAsync(new CodiceIVA
        {
            Codice = "10", Descrizione = "IVA 10%", Aliquota = 10m, Natura = "   ",
        });

        var persistito = await fake.GetByIdAsync(id);
        Assert.Null(persistito!.Natura);
    }

    [Fact]
    public async Task CreaAsync_ObbligoBolloConImponibile_VienePersistitoNull()
    {
        var fake = new FakeCodiceIVARepository();
        var sut = NewSut(fake);

        // Bollo pertinente solo alle operazioni a 0: con aliquota imponibile va
        // forzato a null (non impostato) anche se l'input lo mette a true.
        var id = await sut.CreaAsync(new CodiceIVA
        {
            Codice = "22", Descrizione = "IVA 22%", Aliquota = 22m, Natura = null, ObbligoBollo = true,
        });

        var persistito = await fake.GetByIdAsync(id);
        Assert.Null(persistito!.ObbligoBollo);
    }

    // Operazione esente: l'Obbligo bollo Sì/No si conserva così com'è.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreaAsync_ObbligoBolloConEsente_ConservaSiNo(bool valore)
    {
        var fake = new FakeCodiceIVARepository();
        var sut = NewSut(fake);

        var id = await sut.CreaAsync(new CodiceIVA
        {
            Codice = "N3", Descrizione = "Non imponibile", Aliquota = 0m, Natura = "N3", ObbligoBollo = valore,
        });

        var persistito = await fake.GetByIdAsync(id);
        Assert.Equal(valore, persistito!.ObbligoBollo);
    }

    [Fact]
    public async Task CreaAsync_EsenteSenzaObbligoBollo_LanciaObbligoBolloObbligatorio()
    {
        var sut = NewSut(new FakeCodiceIVARepository());

        // Aliquota 0 + Natura ok ma bollo non scelto (null) → non ammesso.
        var input = new CodiceIVA
        {
            Codice = "N3", Descrizione = "Non imponibile", Aliquota = 0m, Natura = "N3", ObbligoBollo = null,
        };

        var ex = await Assert.ThrowsAsync<CodiceIVAInvalidaException>(() => sut.CreaAsync(input));
        Assert.Equal(CodiceIVAInvalidaMotivo.ObbligoBolloObbligatorio, ex.Motivo);
    }

    [Fact]
    public async Task ElencoAsync_RestituisceSoloAttiviOrdinatiPerCodice()
    {
        var fake = new FakeCodiceIVARepository();
        var sut = NewSut(fake);
        await sut.CreaAsync(Imponibile(codice: "22", descr: "IVA 22%"));
        await sut.CreaAsync(Imponibile(codice: "04", descr: "IVA 4%", aliquota: 4m));
        await sut.CreaAsync(Imponibile(codice: "10", descr: "IVA 10%", aliquota: 10m));

        var elenco = await sut.ElencoAsync();

        Assert.Collection(elenco,
            c => Assert.Equal("04", c.Codice),
            c => Assert.Equal("10", c.Codice),
            c => Assert.Equal("22", c.Codice));
    }

    // =================================================================
    // DELETE: pattern doppia difesa
    // =================================================================

    [Fact]
    public async Task EliminaAsync_SeHasDipendenze_LanciaConDipendenze_NonDisattiva()
    {
        var fake = new FakeCodiceIVARepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Imponibile());
        fake.DipendenzeDa.Add(id);

        await Assert.ThrowsAsync<CodiceIVAConDipendenzeException>(() => sut.EliminaAsync(id));

        var persistito = await fake.GetByIdAsync(id);
        Assert.True(persistito!.IsAttivo);
    }

    [Fact]
    public async Task EliminaAsync_SenzaDipendenze_DisattivaERimuoveDaElenco()
    {
        var fake = new FakeCodiceIVARepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Imponibile());

        await sut.EliminaAsync(id);

        var persistito = await fake.GetByIdAsync(id);
        Assert.NotNull(persistito);
        Assert.False(persistito!.IsAttivo);
        Assert.DoesNotContain(await sut.ElencoAsync(), c => c.IdCodiceIVA == id);
    }

    [Fact]
    public async Task EEliminabileAsync_RispecchiaLoStatoDelleDipendenze()
    {
        var fake = new FakeCodiceIVARepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Imponibile());

        Assert.True(await sut.EEliminabileAsync(id));

        fake.DipendenzeDa.Add(id);
        Assert.False(await sut.EEliminabileAsync(id));
    }
}
