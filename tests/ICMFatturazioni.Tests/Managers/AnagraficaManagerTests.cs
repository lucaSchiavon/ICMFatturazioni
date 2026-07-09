using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;
using ICMFatturazioni.Web.Managers.Interfaces;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test del manager Anagrafica. Tre obiettivi:
///   1) validazione dei campi obbligatori solleva eccezioni tipizzate
///      con il motivo corretto;
///   2) il manager delega al repository senza mutare i dati;
///   3) la DELETE rispetta il pattern doppia difesa (pre-check + sentinel).
/// Niente DB reale: si usa <see cref="FakeAnagraficaRepository"/>.
/// </summary>
public class AnagraficaManagerTests
{
    // -----------------------------------------------------------------
    // Helper: costruisce un'anagrafica valida di default
    // -----------------------------------------------------------------
    private static Anagrafica AnagraficaValida(string? rs = "Acme S.r.l.") => new()
    {
        TipoAnagrafica = TipoAnagrafica.Societa,
        RagioneSociale = rs!,
        SiglaPaese     = "IT",
    };

    // Anagrafica valida con riferimenti amministrativi (Tappa 6). `id` permette
    // di fissare l'IdAnagrafica per i test di AggiornaAsync (coerenza caso Cliente).
    private static Anagrafica ConRiferimenti(
        Guid? idPag = null, Guid? idBanca = null, Guid? idIva = null, Guid id = default) => new()
    {
        IdAnagrafica    = id,
        TipoAnagrafica  = TipoAnagrafica.Societa,
        RagioneSociale  = "Acme S.r.l.",
        SiglaPaese      = "IT",
        IdPag           = idPag,
        IdBancaAppoggio = idBanca,
        IdCodiciIVA     = idIva,
    };

    // Factory del SUT col nuovo costruttore a 5 dipendenze. I fake dei cataloghi
    // sono opzionali: i test che non li toccano usano default vuoti (nessun
    // riferimento → nessuna validazione FK scatta).
    private static AnagraficaManager NewSut(
        FakeAnagraficaRepository fake,
        IAuditManager? audit = null,
        ICodicePagamentoManager? pag = null,
        IBancaAppoggioManager? banca = null,
        ICodiceIVAManager? iva = null)
        => new(
            fake,
            audit ?? new FakeAuditManager(),
            pag ?? new FakeCodicePagamentoManager(),
            banca ?? new FakeBancaAppoggioManager(),
            iva ?? new FakeCodiceIVAManager());

    // =================================================================
    // Audit
    // =================================================================

    [Fact]
    public async Task CreaAsync_RegistraAuditDiCreazione()
    {
        var fake = new FakeAnagraficaRepository();
        var audit = new FakeAuditManager();
        var sut = NewSut(fake, audit);

        var id = await sut.CreaAsync(AnagraficaValida(rs: "Acme S.r.l."));

        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Creazione, voce.Operazione);
        Assert.Equal("Anagrafica", voce.EntityType);
        Assert.Equal(id, voce.EntityId);
        Assert.Equal("Acme S.r.l.", voce.Descrizione);
    }

    [Fact]
    public async Task EliminaAsync_RegistraAuditDiEliminazione()
    {
        var fake = new FakeAnagraficaRepository();
        var audit = new FakeAuditManager();
        var sut = NewSut(fake, audit);
        var id = await sut.CreaAsync(AnagraficaValida(rs: "Da eliminare"));
        audit.Voci.Clear();   // ignoriamo la voce di creazione

        await sut.EliminaAsync(id);

        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Eliminazione, voce.Operazione);
        Assert.Equal(id, voce.EntityId);
    }

    // =================================================================
    // Validazione campi obbligatori
    // =================================================================

    [Fact]
    public async Task CreaAsync_RagioneSocialeVuota_LanciaAnagraficaInvalidaConMotivoRagioneSociale()
    {
        var fake = new FakeAnagraficaRepository();
        var sut = NewSut(fake);
        var input = AnagraficaValida(rs: "");

        var ex = await Assert.ThrowsAsync<AnagraficaInvalidaException>(
            () => sut.CreaAsync(input));

        Assert.Equal(AnagraficaInvalidaMotivo.RagioneSocialeObbligatoria, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_RagioneSocialeWhitespace_LanciaAnagraficaInvalida()
    {
        var fake = new FakeAnagraficaRepository();
        var sut = NewSut(fake);
        var input = AnagraficaValida(rs: "   \t  ");

        var ex = await Assert.ThrowsAsync<AnagraficaInvalidaException>(
            () => sut.CreaAsync(input));

        Assert.Equal(AnagraficaInvalidaMotivo.RagioneSocialeObbligatoria, ex.Motivo);
    }

    // Anagrafica di tipo Ente pubblico (TipoAnagrafica è init-only → si costruisce
    // direttamente, non si può riassegnare dopo la creazione).
    private static Anagrafica AnagraficaEnte() => new()
    {
        TipoAnagrafica = TipoAnagrafica.EntePubblico,
        RagioneSociale = "Comune di Verona",
        SiglaPaese     = "IT",
    };

    [Fact]
    public async Task CreaAsync_EntePubblico_LanciaAnagraficaInvalidaConMotivoEnteNonSupportato()
    {
        var fake = new FakeAnagraficaRepository();
        var sut = NewSut(fake);

        var ex = await Assert.ThrowsAsync<AnagraficaInvalidaException>(
            () => sut.CreaAsync(AnagraficaEnte()));

        Assert.Equal(AnagraficaInvalidaMotivo.EntePubblicoNonSupportato, ex.Motivo);
        // La guardia precede il persist: nessuna anagrafica salvata.
        Assert.Empty(await fake.GetAttiviAsync());
    }

    [Fact]
    public async Task AggiornaAsync_EntePubblico_LanciaAnagraficaInvalida()
    {
        var fake = new FakeAnagraficaRepository();
        var sut = NewSut(fake);

        var ex = await Assert.ThrowsAsync<AnagraficaInvalidaException>(
            () => sut.AggiornaAsync(AnagraficaEnte()));

        Assert.Equal(AnagraficaInvalidaMotivo.EntePubblicoNonSupportato, ex.Motivo);
    }

    [Fact]
    public async Task AggiornaAsync_RagioneSocialeVuota_LanciaAnagraficaInvalida()
    {
        var fake = new FakeAnagraficaRepository();
        var sut = NewSut(fake);
        var input = AnagraficaValida(rs: "");

        var ex = await Assert.ThrowsAsync<AnagraficaInvalidaException>(
            () => sut.AggiornaAsync(input));

        Assert.Equal(AnagraficaInvalidaMotivo.RagioneSocialeObbligatoria, ex.Motivo);
    }

    // =================================================================
    // Happy path: il manager delega correttamente al repository
    // =================================================================

    [Fact]
    public async Task CreaAsync_AnagraficaValida_RestituisceIdEPersisteSulRepository()
    {
        var fake = new FakeAnagraficaRepository();
        var sut = NewSut(fake);

        var id = await sut.CreaAsync(AnagraficaValida());

        Assert.NotEqual(Guid.Empty, id);
        var persistita = await fake.GetByIdAsync(id);
        Assert.NotNull(persistita);
        Assert.Equal("Acme S.r.l.", persistita!.RagioneSociale);
    }

    [Fact]
    public async Task ElencoAsync_RestituisceLeAnagraficheOrdinatePerRagioneSociale()
    {
        var fake = new FakeAnagraficaRepository();
        var sut = NewSut(fake);

        await sut.CreaAsync(AnagraficaValida(rs: "Beta S.p.A."));
        await sut.CreaAsync(AnagraficaValida(rs: "Alfa S.r.l."));
        await sut.CreaAsync(AnagraficaValida(rs: "Gamma S.n.c."));

        var elenco = await sut.ElencoAsync();

        Assert.Collection(elenco,
            a => Assert.Equal("Alfa S.r.l.", a.RagioneSociale),
            a => Assert.Equal("Beta S.p.A.", a.RagioneSociale),
            a => Assert.Equal("Gamma S.n.c.", a.RagioneSociale));
    }

    // =================================================================
    // DELETE: pattern doppia difesa
    // =================================================================

    [Fact]
    public async Task EliminaAsync_SeHasDipendenze_LanciaAnagraficaConDipendenze_NoCallSulRepository()
    {
        var fake = new FakeAnagraficaRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(AnagraficaValida());
        fake.DipendenzeDa.Add(id);

        await Assert.ThrowsAsync<AnagraficaConDipendenzeException>(
            () => sut.EliminaAsync(id));

        // Verifica che la riga NON sia stata rimossa nonostante la
        // chiamata: il pre-check è scattato prima del DELETE.
        Assert.NotNull(await fake.GetByIdAsync(id));
    }

    [Fact]
    public async Task EliminaAsync_SenzaDipendenze_DisattivaLaRiga()
    {
        var fake = new FakeAnagraficaRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(AnagraficaValida());

        await sut.EliminaAsync(id);

        // Soft-delete (ADR D22): la riga resta nel repository ma disattivata,
        // e non compare più nell'elenco (che restituisce solo le attive).
        var persistita = await fake.GetByIdAsync(id);
        Assert.NotNull(persistita);
        Assert.False(persistita!.IsAttivo);
        Assert.DoesNotContain(await sut.ElencoAsync(), a => a.IdAnagrafica == id);
    }

    [Fact]
    public async Task EEliminabileAsync_RispecchiaLoStatoDelleDipendenze()
    {
        var fake = new FakeAnagraficaRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(AnagraficaValida());

        Assert.True(await sut.EEliminabileAsync(id));

        fake.DipendenzeDa.Add(id);
        Assert.False(await sut.EEliminabileAsync(id));
    }

    // =================================================================
    // Validazione riferimenti amministrativi (Tappa 6)
    // =================================================================

    [Fact]
    public async Task CreaAsync_PagamentoInesistente_LanciaConMotivoPagamento()
    {
        var fake = new FakeAnagraficaRepository();
        var sut = NewSut(fake, pag: new FakeCodicePagamentoManager()); // elenco vuoto

        var ex = await Assert.ThrowsAsync<AnagraficaInvalidaException>(
            () => sut.CreaAsync(ConRiferimenti(idPag: Guid.NewGuid())));

        Assert.Equal(AnagraficaInvalidaMotivo.PagamentoInesistente, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_BancaDisattivata_LanciaConMotivoBanca()
    {
        var fake = new FakeAnagraficaRepository();
        var banca = new FakeBancaAppoggioManager();
        var idDisattivata = banca.AggiungiAzienda(attiva: false);
        var sut = NewSut(fake, banca: banca);

        var ex = await Assert.ThrowsAsync<AnagraficaInvalidaException>(
            () => sut.CreaAsync(ConRiferimenti(idBanca: idDisattivata)));

        Assert.Equal(AnagraficaInvalidaMotivo.BancaInesistente, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_CodiceIvaDisattivato_LanciaConMotivoCodiceIva()
    {
        var fake = new FakeAnagraficaRepository();
        var iva = new FakeCodiceIVAManager();
        var idDisattivato = iva.Aggiungi(attivo: false);
        var sut = NewSut(fake, iva: iva);

        var ex = await Assert.ThrowsAsync<AnagraficaInvalidaException>(
            () => sut.CreaAsync(ConRiferimenti(idIva: idDisattivato)));

        Assert.Equal(AnagraficaInvalidaMotivo.CodiceIVAInesistente, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_PagamentoAzienda_BancaAzienda_Ok()
    {
        var fake = new FakeAnagraficaRepository();
        var pag = new FakeCodicePagamentoManager();
        var banca = new FakeBancaAppoggioManager();
        var idPag = pag.Aggiungi(FlagBanca.Azienda);
        var idBanca = banca.AggiungiAzienda();
        var sut = NewSut(fake, pag: pag, banca: banca);

        var id = await sut.CreaAsync(ConRiferimenti(idPag: idPag, idBanca: idBanca));

        var persistita = await fake.GetByIdAsync(id);
        Assert.Equal(idBanca, persistita!.IdBancaAppoggio);
    }

    [Fact]
    public async Task CreaAsync_PagamentoAzienda_BancaDelCliente_LanciaIncoerenza()
    {
        var fake = new FakeAnagraficaRepository();
        var pag = new FakeCodicePagamentoManager();
        var banca = new FakeBancaAppoggioManager();
        var idPag = pag.Aggiungi(FlagBanca.Azienda);
        var idBanca = banca.AggiungiCliente(Guid.NewGuid());   // banca di un cliente
        var sut = NewSut(fake, pag: pag, banca: banca);

        var ex = await Assert.ThrowsAsync<AnagraficaInvalidaException>(
            () => sut.CreaAsync(ConRiferimenti(idPag: idPag, idBanca: idBanca)));

        Assert.Equal(AnagraficaInvalidaMotivo.BancaNonCoerenteColPagamento, ex.Motivo);
    }

    [Fact]
    public async Task AggiornaAsync_PagamentoCliente_BancaDelCliente_Ok()
    {
        var fake = new FakeAnagraficaRepository();
        var pag = new FakeCodicePagamentoManager();
        var banca = new FakeBancaAppoggioManager();
        var idPag = pag.Aggiungi(FlagBanca.Cliente);
        var sut = NewSut(fake, pag: pag, banca: banca);

        // Prima creo l'anagrafica (id noto), poi le banche del cliente sono
        // associabili: è esattamente il "flusso fluido" del caso Cliente.
        var idAnag = await sut.CreaAsync(AnagraficaValida());
        var idBanca = banca.AggiungiCliente(idAnag);

        await sut.AggiornaAsync(ConRiferimenti(idPag: idPag, idBanca: idBanca, id: idAnag));

        var persistita = await fake.GetByIdAsync(idAnag);
        Assert.Equal(idBanca, persistita!.IdBancaAppoggio);
    }

    [Fact]
    public async Task AggiornaAsync_PagamentoCliente_BancaDiAltroCliente_LanciaIncoerenza()
    {
        var fake = new FakeAnagraficaRepository();
        var pag = new FakeCodicePagamentoManager();
        var banca = new FakeBancaAppoggioManager();
        var idPag = pag.Aggiungi(FlagBanca.Cliente);
        var sut = NewSut(fake, pag: pag, banca: banca);

        var idAnag = await sut.CreaAsync(AnagraficaValida());
        var idBancaAltro = banca.AggiungiCliente(Guid.NewGuid());   // banca di ALTRO cliente

        var ex = await Assert.ThrowsAsync<AnagraficaInvalidaException>(
            () => sut.AggiornaAsync(ConRiferimenti(idPag: idPag, idBanca: idBancaAltro, id: idAnag)));

        Assert.Equal(AnagraficaInvalidaMotivo.BancaNonCoerenteColPagamento, ex.Motivo);
    }

    [Fact]
    public async Task AggiornaAsync_AggiungePagamento_AuditDiffContieneIdPag()
    {
        var fake = new FakeAnagraficaRepository();
        var audit = new FakeAuditManager();
        var pag = new FakeCodicePagamentoManager();
        var idPag = pag.Aggiungi(FlagBanca.Azienda);
        var sut = NewSut(fake, audit, pag: pag);

        var id = await sut.CreaAsync(AnagraficaValida());
        audit.Voci.Clear();   // ignoriamo la creazione

        await sut.AggiornaAsync(ConRiferimenti(idPag: idPag, id: id));

        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Modifica, voce.Operazione);
        Assert.Contains("IdPag", voce.Dati);   // il diff registra il campo cambiato
    }

    // =================================================================
    // Conversione TipoAnagrafica ↔ char (helper)
    // =================================================================

    [Theory]
    [InlineData(TipoAnagrafica.Societa, 'S')]
    [InlineData(TipoAnagrafica.Privato, 'P')]
    [InlineData(TipoAnagrafica.EntePubblico, 'E')]
    public void TipoAnagrafica_ToDbCode_RestituisceIlCarattereAtteso(TipoAnagrafica tipo, char atteso)
    {
        Assert.Equal(atteso, tipo.ToDbCode());
    }

    [Theory]
    [InlineData('S', TipoAnagrafica.Societa)]
    [InlineData('P', TipoAnagrafica.Privato)]
    [InlineData('E', TipoAnagrafica.EntePubblico)]
    public void TipoAnagrafica_FromDbCode_RestituisceLEnumAtteso(char code, TipoAnagrafica atteso)
    {
        Assert.Equal(atteso, TipoAnagraficaExtensions.FromDbCode(code));
    }

    [Fact]
    public void TipoAnagrafica_FromDbCode_CarattereInatteso_LanciaArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TipoAnagraficaExtensions.FromDbCode('X'));
    }
}
