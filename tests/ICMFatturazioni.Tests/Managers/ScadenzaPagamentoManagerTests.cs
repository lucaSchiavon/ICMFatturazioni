using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;
using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test del manager ScadenzaPagamento: validazione campi, protezione HasFattura, audit.
/// </summary>
public class ScadenzaPagamentoManagerTests
{
    private static readonly Guid IdDettaglio = Guid.NewGuid();

    private static ScadenzaPagamento Sca(
        DateOnly? data    = null,
        decimal   importo = 500m,
        Guid?     idDett  = null) => new()
    {
        IdAttivitaDettaglio = idDett ?? IdDettaglio,
        DataScadenza        = data   ?? new DateOnly(2025, 12, 31),
        Importo             = importo,
    };

    private static (ScadenzaPagamentoManager sut, FakeScadenzaPagamentoRepository fakeRepo, FakeAttivitaDettaglioRepository fakeDett)
        NewSut(FakeAuditManager? audit = null)
    {
        var fakeRepo = new FakeScadenzaPagamentoRepository();
        var fakeDett = new FakeAttivitaDettaglioRepository();
        // Pre-popola il dettaglio parent in modo che il Manager lo trovi.
        fakeDett.InsertAsync(new AttivitaDettaglio
        {
            IdAttivitaDettaglio     = IdDettaglio,
            IdAttivita              = Guid.NewGuid(),
            IdTipoDettaglioAttivita = Guid.NewGuid(),
            Ordine                  = 1,
            DescrizioneDettaglio    = "Disciplinare",
            Importo                 = 1000m,
        }).Wait();
        var sut = new ScadenzaPagamentoManager(fakeRepo, fakeDett, audit ?? new FakeAuditManager());
        return (sut, fakeRepo, fakeDett);
    }

    // -------------------------------------------------------------------------
    // Creazione
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreaAsync_Valido_RestituisceId()
    {
        var (sut, fakeRepo, _) = NewSut();

        var id = await sut.CreaAsync(Sca());

        Assert.NotEqual(Guid.Empty, id);
        var s = await fakeRepo.GetByIdAsync(id);
        Assert.NotNull(s);
        Assert.Equal(500m, s.Importo);
    }

    [Fact]
    public async Task CreaAsync_RegistraAuditCreazione()
    {
        var audit = new FakeAuditManager();
        var (sut, _, _) = NewSut(audit);

        var id = await sut.CreaAsync(Sca());

        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Creazione, voce.Operazione);
        Assert.Equal("ScadenzaPagamento", voce.EntityType);
        Assert.Equal(id, voce.EntityId);
    }

    // -------------------------------------------------------------------------
    // Validazione campi
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreaAsync_DataDefault_LanciaDataObbligatoria()
    {
        var (sut, _, _) = NewSut();
        // DataScadenza non impostata: vale default(DateOnly) = 0001-01-01, considerata assente.
        var scadenza = new ScadenzaPagamento
        {
            IdAttivitaDettaglio = IdDettaglio,
            Importo             = 500m,
        };
        var ex = await Assert.ThrowsAsync<ScadenzaPagamentoInvalidaException>(
            () => sut.CreaAsync(scadenza));
        Assert.Equal(ScadenzaPagamentoMotivoInvalido.DataObbligatoria, ex.Motivo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task CreaAsync_ImportoNonPositivo_LanciaImportoNonValido(decimal importo)
    {
        var (sut, _, _) = NewSut();
        var ex = await Assert.ThrowsAsync<ScadenzaPagamentoInvalidaException>(
            () => sut.CreaAsync(Sca(importo: importo)));
        Assert.Equal(ScadenzaPagamentoMotivoInvalido.ImportoNonValido, ex.Motivo);
    }

    // -------------------------------------------------------------------------
    // Somma scadenze ≤ importo dettaglio (ripartizione completa)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreaAsync_SommaSupererebbeImporto_LanciaSommaEccedeImporto()
    {
        // Dettaglio = 1000. Prima 700 (ok), poi 400 → 1100 > 1000 → rifiutata.
        var (sut, _, _) = NewSut();
        await sut.CreaAsync(Sca(importo: 700m));

        var ex = await Assert.ThrowsAsync<ScadenzaPagamentoInvalidaException>(
            () => sut.CreaAsync(Sca(importo: 400m)));
        Assert.Equal(ScadenzaPagamentoMotivoInvalido.SommaEccedeImporto, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_SommaEsattaImporto_Consentita()
    {
        // Dettaglio = 1000. 600 + 400 = 1000 esatti → entrambe accettate.
        var (sut, fakeRepo, _) = NewSut();
        await sut.CreaAsync(Sca(importo: 600m));
        await sut.CreaAsync(Sca(importo: 400m));

        var tutte = await fakeRepo.GetByDettaglioAsync(IdDettaglio);
        Assert.Equal(1000m, tutte.Sum(s => s.Importo));
    }

    [Fact]
    public async Task AggiornaAsync_SommaSupererebbeImporto_LanciaSommaEccedeImporto()
    {
        // Dettaglio = 1000. 600 + 300 = 900. Aggiornare la 300 a 500 → 1100 > 1000 → rifiutata.
        var (sut, _, _) = NewSut();
        await sut.CreaAsync(Sca(importo: 600m));
        var id300 = await sut.CreaAsync(Sca(importo: 300m));

        var aggiornata = Sca(importo: 500m);
        aggiornata.IdScadenza = id300;

        var ex = await Assert.ThrowsAsync<ScadenzaPagamentoInvalidaException>(
            () => sut.AggiornaAsync(aggiornata));
        Assert.Equal(ScadenzaPagamentoMotivoInvalido.SommaEccedeImporto, ex.Motivo);
    }

    [Fact]
    public async Task AggiornaAsync_EscludeLaScadenzaStessa_Consentita()
    {
        // Dettaglio = 1000. Unica scadenza da 1000; aggiornarla a 800 deve passare
        // (la somma esclude la scadenza che si sta riscrivendo).
        var (sut, fakeRepo, _) = NewSut();
        var id = await sut.CreaAsync(Sca(importo: 1000m));

        var aggiornata = Sca(importo: 800m);
        aggiornata.IdScadenza = id;
        await sut.AggiornaAsync(aggiornata);

        Assert.Equal(800m, (await fakeRepo.GetByIdAsync(id))!.Importo);
    }

    // -------------------------------------------------------------------------
    // Protezione HasFattura
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreaAsync_DettaglioFatturato_LanciaDettaglioFatturato()
    {
        var (sut, _, fakeDett) = NewSut();
        fakeDett.SetHasFattura(IdDettaglio, true);

        var ex = await Assert.ThrowsAsync<ScadenzaPagamentoInvalidaException>(
            () => sut.CreaAsync(Sca()));
        Assert.Equal(ScadenzaPagamentoMotivoInvalido.DettaglioFatturato, ex.Motivo);
    }

    [Fact]
    public async Task EliminaAsync_DettaglioFatturato_LanciaDettaglioFatturato()
    {
        var (sut, fakeRepo, fakeDett) = NewSut();
        var id = await sut.CreaAsync(Sca());
        fakeDett.SetHasFattura(IdDettaglio, true);

        var ex = await Assert.ThrowsAsync<ScadenzaPagamentoInvalidaException>(
            () => sut.EliminaAsync(id));
        Assert.Equal(ScadenzaPagamentoMotivoInvalido.DettaglioFatturato, ex.Motivo);
        Assert.True((await fakeRepo.GetByIdAsync(id))!.IsAttivo);
    }

    [Fact]
    public async Task EliminaAsync_SenzaFattura_DisattivaEAudita()
    {
        var audit = new FakeAuditManager();
        var (sut, fakeRepo, _) = NewSut(audit);
        var id = await sut.CreaAsync(Sca());
        audit.Voci.Clear();

        await sut.EliminaAsync(id);

        Assert.False((await fakeRepo.GetByIdAsync(id))!.IsAttivo);
        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Eliminazione, voce.Operazione);
    }

    // -------------------------------------------------------------------------
    // Lock a livello rata (rata evasa da un avviso di fattura)
    // -------------------------------------------------------------------------

    // Semina direttamente nel repo una rata già "evasa" (IdAvvisoRiga valorizzato):
    // simula lo stato dopo l'emissione di un avviso che l'ha consumata.
    private static ScadenzaPagamento ScaEvasa(decimal importo = 500m)
    {
        return new ScadenzaPagamento
        {
            IdScadenza          = Guid.NewGuid(),
            IdAttivitaDettaglio = IdDettaglio,
            DataScadenza        = new DateOnly(2025, 12, 31),
            Importo             = importo,
            IdAvvisoRiga        = Guid.NewGuid(),
        };
    }

    [Fact]
    public async Task AggiornaAsync_RataEvasa_LanciaRataEvasa()
    {
        var (sut, fakeRepo, _) = NewSut();
        var evasa = ScaEvasa();
        await fakeRepo.InsertAsync(evasa);

        var modifica = Sca(importo: 800m);
        modifica.IdScadenza = evasa.IdScadenza;

        var ex = await Assert.ThrowsAsync<ScadenzaPagamentoInvalidaException>(
            () => sut.AggiornaAsync(modifica));
        Assert.Equal(ScadenzaPagamentoMotivoInvalido.RataEvasa, ex.Motivo);
        // La rata resta invariata (500 €, non 800 €).
        Assert.Equal(500m, (await fakeRepo.GetByIdAsync(evasa.IdScadenza))!.Importo);
    }

    [Fact]
    public async Task EliminaAsync_RataEvasa_LanciaRataEvasa()
    {
        var (sut, fakeRepo, _) = NewSut();
        var evasa = ScaEvasa();
        await fakeRepo.InsertAsync(evasa);

        var ex = await Assert.ThrowsAsync<ScadenzaPagamentoInvalidaException>(
            () => sut.EliminaAsync(evasa.IdScadenza));
        Assert.Equal(ScadenzaPagamentoMotivoInvalido.RataEvasa, ex.Motivo);
        Assert.True((await fakeRepo.GetByIdAsync(evasa.IdScadenza))!.IsAttivo);
    }

    // -------------------------------------------------------------------------
    // Elenco
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ElencoPerDettaglioAsync_OrdinaPerDataAsc()
    {
        var (sut, _, _) = NewSut();
        // Importi che ripartiscono i 1000 € del dettaglio senza eccederli.
        await sut.CreaAsync(Sca(data: new DateOnly(2025, 12, 31), importo: 300m));
        await sut.CreaAsync(Sca(data: new DateOnly(2025,  6, 30), importo: 300m));
        await sut.CreaAsync(Sca(data: new DateOnly(2026,  3, 31), importo: 400m));

        var lista = await sut.ElencoPerDettaglioAsync(IdDettaglio);

        Assert.Equal(3, lista.Count);
        Assert.Equal(new DateOnly(2025,  6, 30), lista[0].DataScadenza);
        Assert.Equal(new DateOnly(2025, 12, 31), lista[1].DataScadenza);
        Assert.Equal(new DateOnly(2026,  3, 31), lista[2].DataScadenza);
    }

    // -------------------------------------------------------------------------
    // Report scadenzario (maschera Stampa scadenze)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ReportScadenzarioAsync_PassaFiltroVerbatimEOggiAlRepository()
    {
        var (sut, fakeRepo, _) = NewSut();
        fakeRepo.ReportRighe.Add(new ScadenzaReport(
            DataScadenza:             new DateOnly(2026, 7, 31),
            Importo:                  1500m,
            IsEvasa:                  false,
            AvvisoDataEvasione:       null,
            NotaScadenza:             null,
            TipoCliente:              TipoAnagrafica.Societa,
            ClienteRagioneSociale:    "ROSSI SRL",
            TipoAttivitaDescrizione:  "PROGETTAZIONI",
            NumeroAttivita:           "872",
            DescrizioneAttivita:      "Tettoia",
            TipoDettaglioDescrizione: "DISCIPLINARE",
            DescrizioneDettaglio:     "Acconto progetto"));

        var filtro = new FiltroScadenzario(
            TipoCliente: TipoAnagrafica.Privato,
            DallaData:   new DateOnly(2026, 1, 1),
            Scadute:     FiltroScadute.SoloNonScadute,
            Evase:       FiltroEvase.SoloNonEvase);

        var righe = await sut.ReportScadenzarioAsync(filtro);

        // Il manager è pass-through sul filtro e fissa "oggi" alla data odierna.
        Assert.Single(righe);
        Assert.Same(filtro, fakeRepo.UltimoFiltroReport);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), fakeRepo.UltimaOggiReport);
    }
}
