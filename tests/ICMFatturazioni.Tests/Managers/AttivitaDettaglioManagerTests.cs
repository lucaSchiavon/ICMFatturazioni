using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test del manager AttivitaDettaglio: validazione campi, ordine automatico,
/// protezione HasFattura, riordinamento e audit.
/// </summary>
public class AttivitaDettaglioManagerTests
{
    private static readonly Guid IdAttivita       = Guid.NewGuid();
    private static readonly Guid IdTipoDettaglio  = Guid.NewGuid();

    private static AttivitaDettaglio Det(
        string     descr      = "Disciplinare",
        decimal    importo    = 1000m,
        Guid?      idAttivita = null,
        Guid?      idTipo     = null,
        DateOnly?  termine    = null) => new()
    {
        IdAttivita              = idAttivita ?? IdAttivita,
        IdTipoDettaglioAttivita = idTipo     ?? IdTipoDettaglio,
        DescrizioneDettaglio    = descr,
        Importo                 = importo,
        TerminePrevisto         = termine ?? new DateOnly(2026, 7, 31),
    };

    private static AttivitaDettaglioManager NewSut(
        FakeAttivitaDettaglioRepository fake,
        FakeAuditManager? audit = null,
        FakeScadenzaPagamentoRepository? scadenzeRepo = null)
    {
        audit ??= new FakeAuditManager();
        // ScadenzaPagamentoManager reale con fake: condivide lo stesso repo dettagli
        // (per il check HasFattura) e lo stesso audit del manager sotto test.
        var scadenzeMgr = new ScadenzaPagamentoManager(
            scadenzeRepo ?? new FakeScadenzaPagamentoRepository(), fake, audit);
        return new AttivitaDettaglioManager(fake, scadenzeMgr, audit);
    }

    // -------------------------------------------------------------------------
    // Creazione e ordine automatico
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreaAsync_Valido_RestituisceIdEAssegnaOrdine()
    {
        var fake = new FakeAttivitaDettaglioRepository();
        var sut  = NewSut(fake);

        var id = await sut.CreaAsync(Det());

        Assert.NotEqual(Guid.Empty, id);
        var d = await fake.GetByIdAsync(id);
        Assert.NotNull(d);
        Assert.Equal(1, d.Ordine);
        Assert.Equal("Disciplinare", d.DescrizioneDettaglio);
    }

    [Fact]
    public async Task CreaAsync_PiuDettagli_OrdineProgressivo()
    {
        var fake = new FakeAttivitaDettaglioRepository();
        var sut  = NewSut(fake);

        var id1 = await sut.CreaAsync(Det(descr: "Prima"));
        var id2 = await sut.CreaAsync(Det(descr: "Seconda"));
        var id3 = await sut.CreaAsync(Det(descr: "Terza"));

        Assert.Equal(1, (await fake.GetByIdAsync(id1))!.Ordine);
        Assert.Equal(2, (await fake.GetByIdAsync(id2))!.Ordine);
        Assert.Equal(3, (await fake.GetByIdAsync(id3))!.Ordine);
    }

    // Regressione: dopo l'eliminazione (soft-delete) di un dettaglio il suo Ordine
    // resta "occupato" nel vincolo UNIQUE (IdAttivita, Ordine). Il nuovo dettaglio
    // NON deve riusare quell'Ordine, altrimenti l'INSERT viola il vincolo.
    [Fact]
    public async Task CreaAsync_DopoEliminazione_NonRiusaOrdineDellaRigaCancellata()
    {
        var fake = new FakeAttivitaDettaglioRepository();
        var sut  = NewSut(fake);

        var id1 = await sut.CreaAsync(Det(descr: "Prima"));   // Ordine 1
        var id2 = await sut.CreaAsync(Det(descr: "Seconda"));  // Ordine 2
        var id3 = await sut.CreaAsync(Det(descr: "Terza"));    // Ordine 3

        await sut.EliminaAsync(id3); // soft-delete: Ordine 3 resta occupato

        var id4 = await sut.CreaAsync(Det(descr: "Quarta"));

        // Deve prendere Ordine 4, non 3 (che è ancora usato dalla riga soft-deletata).
        Assert.Equal(4, (await fake.GetByIdAsync(id4))!.Ordine);
    }

    [Fact]
    public async Task CreaAsync_RegistraAuditCreazione()
    {
        var fake  = new FakeAttivitaDettaglioRepository();
        var audit = new FakeAuditManager();
        var sut   = NewSut(fake, audit);

        var id = await sut.CreaAsync(Det());

        // La creazione genera due voci di audit: il dettaglio e la scadenza di default.
        var voce = Assert.Single(audit.Voci, v => v.EntityType == "AttivitaDettaglio");
        Assert.Equal(AuditOperazione.Creazione, voce.Operazione);
        Assert.Equal(id, voce.EntityId);
    }

    // La creazione di un dettaglio deve generare automaticamente una scadenza di default
    // che copre l'intero importo, con data = termine previsto (screenshot A3.png).
    [Fact]
    public async Task CreaAsync_GeneraScadenzaDiDefaultPariAllImporto()
    {
        var fake     = new FakeAttivitaDettaglioRepository();
        var scadRepo = new FakeScadenzaPagamentoRepository();
        var sut      = NewSut(fake, scadenzeRepo: scadRepo);
        var termine  = new DateOnly(2026, 7, 31);

        var id = await sut.CreaAsync(Det(importo: 600m, termine: termine));

        var scadenze = await scadRepo.GetByDettaglioAsync(id);
        var s = Assert.Single(scadenze);
        Assert.Equal(600m, s.Importo);
        Assert.Equal(termine, s.DataScadenza);
    }

    // -------------------------------------------------------------------------
    // Validazione campi obbligatori
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreaAsync_TipoVuoto_LanciaTipoObbligatorio()
    {
        var sut = NewSut(new FakeAttivitaDettaglioRepository());
        var ex  = await Assert.ThrowsAsync<AttivitaDettaglioInvalidaException>(
            () => sut.CreaAsync(Det(idTipo: Guid.Empty)));
        Assert.Equal(AttivitaDettaglioMotivoInvalido.TipoDettaglioObbligatorio, ex.Motivo);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreaAsync_DescrizioneVuota_LanciaDescrizioneObbligatoria(string descr)
    {
        var sut = NewSut(new FakeAttivitaDettaglioRepository());
        var ex  = await Assert.ThrowsAsync<AttivitaDettaglioInvalidaException>(
            () => sut.CreaAsync(Det(descr: descr)));
        Assert.Equal(AttivitaDettaglioMotivoInvalido.DescrizioneObbligatoria, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_TermineNullo_LanciaTerminePrevistoObbligatorio()
    {
        var sut       = NewSut(new FakeAttivitaDettaglioRepository());
        var dettaglio = new AttivitaDettaglio
        {
            IdAttivita              = IdAttivita,
            IdTipoDettaglioAttivita = IdTipoDettaglio,
            DescrizioneDettaglio    = "Disciplinare",
            Importo                 = 1000m,
            TerminePrevisto         = null,
        };
        var ex = await Assert.ThrowsAsync<AttivitaDettaglioInvalidaException>(
            () => sut.CreaAsync(dettaglio));
        Assert.Equal(AttivitaDettaglioMotivoInvalido.TerminePrevistoObbligatorio, ex.Motivo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreaAsync_ImportoNonPositivo_LanciaImportoNonValido(decimal importo)
    {
        var sut = NewSut(new FakeAttivitaDettaglioRepository());
        var ex  = await Assert.ThrowsAsync<AttivitaDettaglioInvalidaException>(
            () => sut.CreaAsync(Det(importo: importo)));
        Assert.Equal(AttivitaDettaglioMotivoInvalido.ImportoNonValido, ex.Motivo);
    }

    // -------------------------------------------------------------------------
    // Protezione HasFattura
    // -------------------------------------------------------------------------

    [Fact]
    public async Task AggiornaAsync_HasFattura_LanciaHasFattura()
    {
        var fake = new FakeAttivitaDettaglioRepository();
        var sut  = NewSut(fake);
        var id   = await sut.CreaAsync(Det());
        fake.SetHasFattura(id, true);

        var da = await fake.GetByIdAsync(id);
        var ex = await Assert.ThrowsAsync<AttivitaDettaglioInvalidaException>(
            () => sut.AggiornaAsync(da!));
        Assert.Equal(AttivitaDettaglioMotivoInvalido.HasFattura, ex.Motivo);
    }

    [Fact]
    public async Task EliminaAsync_HasFattura_LanciaHasFattura()
    {
        var fake = new FakeAttivitaDettaglioRepository();
        var sut  = NewSut(fake);
        var id   = await sut.CreaAsync(Det());
        fake.SetHasFattura(id, true);

        var ex = await Assert.ThrowsAsync<AttivitaDettaglioInvalidaException>(
            () => sut.EliminaAsync(id));
        Assert.Equal(AttivitaDettaglioMotivoInvalido.HasFattura, ex.Motivo);
        Assert.True((await fake.GetByIdAsync(id))!.IsAttivo);
    }

    [Fact]
    public async Task EliminaAsync_SenzaFattura_DisattivaEAudita()
    {
        var fake  = new FakeAttivitaDettaglioRepository();
        var audit = new FakeAuditManager();
        var sut   = NewSut(fake, audit);
        var id    = await sut.CreaAsync(Det());
        audit.Voci.Clear();

        await sut.EliminaAsync(id);

        Assert.False((await fake.GetByIdAsync(id))!.IsAttivo);
        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Eliminazione, voce.Operazione);
    }

    // -------------------------------------------------------------------------
    // Riordinamento
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SpostaSuAsync_SecondaRiga_DiventaPrima()
    {
        var fake = new FakeAttivitaDettaglioRepository();
        var sut  = NewSut(fake);
        var id1  = await sut.CreaAsync(Det(descr: "Prima"));
        var id2  = await sut.CreaAsync(Det(descr: "Seconda"));

        await sut.SpostaSuAsync(id2);

        var lista = await sut.ElencoPerAttivitaAsync(IdAttivita);
        Assert.Equal("Seconda", lista[0].DescrizioneDettaglio);
        Assert.Equal("Prima",   lista[1].DescrizioneDettaglio);
    }

    [Fact]
    public async Task SpostaSuAsync_GiaPrima_Noop()
    {
        var fake = new FakeAttivitaDettaglioRepository();
        var sut  = NewSut(fake);
        var id1  = await sut.CreaAsync(Det(descr: "Prima"));
        var id2  = await sut.CreaAsync(Det(descr: "Seconda"));

        await sut.SpostaSuAsync(id1); // no-op

        var lista = await sut.ElencoPerAttivitaAsync(IdAttivita);
        Assert.Equal("Prima",   lista[0].DescrizioneDettaglio);
        Assert.Equal("Seconda", lista[1].DescrizioneDettaglio);
    }

    [Fact]
    public async Task SpostaGiuAsync_PrimaRiga_DiventaSeconda()
    {
        var fake = new FakeAttivitaDettaglioRepository();
        var sut  = NewSut(fake);
        var id1  = await sut.CreaAsync(Det(descr: "Prima"));
        var id2  = await sut.CreaAsync(Det(descr: "Seconda"));

        await sut.SpostaGiuAsync(id1);

        var lista = await sut.ElencoPerAttivitaAsync(IdAttivita);
        Assert.Equal("Seconda", lista[0].DescrizioneDettaglio);
        Assert.Equal("Prima",   lista[1].DescrizioneDettaglio);
    }

    [Fact]
    public async Task SpostaGiuAsync_GiaUltima_Noop()
    {
        var fake = new FakeAttivitaDettaglioRepository();
        var sut  = NewSut(fake);
        var id1  = await sut.CreaAsync(Det(descr: "Prima"));
        var id2  = await sut.CreaAsync(Det(descr: "Seconda"));

        await sut.SpostaGiuAsync(id2); // no-op

        var lista = await sut.ElencoPerAttivitaAsync(IdAttivita);
        Assert.Equal("Prima",   lista[0].DescrizioneDettaglio);
        Assert.Equal("Seconda", lista[1].DescrizioneDettaglio);
    }

    // -------------------------------------------------------------------------
    // Elenco
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ElencoPerAttivitaAsync_OrdinaPerOrdineAsc()
    {
        var fake = new FakeAttivitaDettaglioRepository();
        var sut  = NewSut(fake);
        await sut.CreaAsync(Det(descr: "A"));
        await sut.CreaAsync(Det(descr: "B"));
        await sut.CreaAsync(Det(descr: "C"));

        var lista = await sut.ElencoPerAttivitaAsync(IdAttivita);

        Assert.Equal(3, lista.Count);
        Assert.Equal("A", lista[0].DescrizioneDettaglio);
        Assert.Equal("B", lista[1].DescrizioneDettaglio);
        Assert.Equal("C", lista[2].DescrizioneDettaglio);
    }
}
