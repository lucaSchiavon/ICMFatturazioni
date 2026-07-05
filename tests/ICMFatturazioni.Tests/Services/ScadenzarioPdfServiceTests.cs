using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Models;
using ICMFatturazioni.Web.Services;

namespace ICMFatturazioni.Tests.Services;

/// <summary>
/// Test del servizio PDF "Scadenziario attività clienti": rendering (%PDF) sugli
/// scenari principali (vuoto, multi-anno/mese, con evase) e composizione della
/// descrizione del filtro per il piè di pagina (funzione pura).
/// I manager sono fake minimali: il servizio legge solo le righe del report e,
/// se il filtro li referenzia, il nome di cliente/tipo attività.
/// </summary>
public class ScadenzarioPdfServiceTests
{
    // ── Fake minimali dei 3 manager richiesti dal servizio ─────────────────

    private sealed class FakeScadenzaManager : IScadenzaPagamentoManager
    {
        public List<ScadenzaReport> Righe { get; } = new();

        public Task<IReadOnlyList<ScadenzaReport>> ReportScadenzarioAsync(FiltroScadenzario filtro, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ScadenzaReport>>(Righe.ToList());

        public Task<IReadOnlyList<ScadenzaPagamento>> ElencoPerDettaglioAsync(Guid idAttivitaDettaglio, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<Guid> CreaAsync(ScadenzaPagamento scadenza, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task AggiornaAsync(ScadenzaPagamento scadenza, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task EliminaAsync(Guid idScadenza, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeAnagraficaManagerPdf : IAnagraficaManager
    {
        public Anagrafica? Cliente { get; set; }

        public Task<Anagrafica?> GetByIdAsync(Guid idAnagrafica, CancellationToken cancellationToken = default)
            => Task.FromResult(Cliente);

        public Task<IReadOnlyList<Anagrafica>> ElencoAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<Guid> CreaAsync(Anagrafica anagrafica, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task AggiornaAsync(Anagrafica anagrafica, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task EliminaAsync(Guid idAnagrafica, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<bool> EEliminabileAsync(Guid idAnagrafica, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeTipoAttivitaManagerPdf : ITipoAttivitaManager
    {
        public TipoAttivita? Tipo { get; set; }

        public Task<TipoAttivita?> GetByIdAsync(Guid idTipoAttivita, CancellationToken cancellationToken = default)
            => Task.FromResult(Tipo);

        public Task<IReadOnlyList<TipoAttivita>> ElencoAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<Guid> CreaAsync(TipoAttivita tipo, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task AggiornaAsync(TipoAttivita tipo, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task EliminaAsync(Guid idTipoAttivita, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<bool> EEliminabileAsync(Guid idTipoAttivita, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    // ── Helper ──────────────────────────────────────────────────────────────

    private static ScadenzaReport Riga(
        DateOnly? data     = null,
        decimal   importo  = 1500m,
        bool      evasa    = false,
        DateOnly? avvisoIl = null,
        string?   nota     = null) => new(
        DataScadenza:             data ?? new DateOnly(2026, 7, 31),
        Importo:                  importo,
        IsEvasa:                  evasa,
        AvvisoDataEvasione:       avvisoIl,
        NotaScadenza:             nota,
        TipoCliente:              TipoAnagrafica.Societa,
        ClienteRagioneSociale:    "GARDACAMP S.R.L.",
        TipoAttivitaDescrizione:  "PROGETTAZIONI",
        NumeroAttivita:           "799",
        DescrizioneAttivita:      "Ampliamento del villaggio del Garda.",
        TipoDettaglioDescrizione: "DISCIPLINARE",
        DescrizioneDettaglio:     "Direzione lavori opere edili");

    private static (ScadenzarioPdfService sut, FakeScadenzaManager scadenze, FakeAnagraficaManagerPdf anagrafiche, FakeTipoAttivitaManagerPdf tipi) NewSut()
    {
        var scadenze    = new FakeScadenzaManager();
        var anagrafiche = new FakeAnagraficaManagerPdf();
        var tipi        = new FakeTipoAttivitaManagerPdf();
        return (new ScadenzarioPdfService(scadenze, anagrafiche, tipi), scadenze, anagrafiche, tipi);
    }

    private static void AssertIsPdf(byte[] bytes)
    {
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 4, "PDF troppo corto.");
        // Magic number "%PDF" in testa al file.
        Assert.Equal((byte)'%', bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'D', bytes[2]);
        Assert.Equal((byte)'F', bytes[3]);
    }

    // ── Rendering ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GeneraAsync_NessunaScadenza_ProduceComunquePdf()
    {
        var (sut, _, _, _) = NewSut();

        var pdf = await sut.GeneraAsync(new FiltroScadenzario());

        AssertIsPdf(pdf);
    }

    [Fact]
    public async Task GeneraAsync_ScadenzeSuPiuAnniEMesi_ProducePdf()
    {
        var (sut, scadenze, _, _) = NewSut();
        // Due anni, tre mesi: esercita gruppi anno/mese e tutti i totali.
        scadenze.Righe.Add(Riga(data: new DateOnly(2025, 7, 28), importo: 12456m));
        scadenze.Righe.Add(Riga(data: new DateOnly(2025, 8, 31), importo: 766m, nota: "saldo 25%"));
        scadenze.Righe.Add(Riga(data: new DateOnly(2026, 3, 2),  importo: 13677m));

        var pdf = await sut.GeneraAsync(new FiltroScadenzario());

        AssertIsPdf(pdf);
    }

    [Fact]
    public async Task GeneraAsync_ConEvase_ProducePdf()
    {
        var (sut, scadenze, _, _) = NewSut();
        scadenze.Righe.Add(Riga(evasa: true, avvisoIl: new DateOnly(2026, 7, 2)));
        scadenze.Righe.Add(Riga(evasa: true));   // evasa senza data avviso (nav mancante)

        var pdf = await sut.GeneraAsync(new FiltroScadenzario(Evase: FiltroEvase.SoloEvase));

        AssertIsPdf(pdf);
    }

    // ── Descrizione filtro per il piè di pagina (funzione pura) ─────────────

    [Fact]
    public void ComponiDescrizioneFiltro_FiltroVuoto_TuttiClientiTutteAttivita()
    {
        var testo = ScadenzarioPdfService.ComponiDescrizioneFiltro(new FiltroScadenzario(), null, null);

        Assert.Equal("Tutti i Clienti - Tutte le Attività", testo);
    }

    [Fact]
    public void ComponiDescrizioneFiltro_ClientePuntuale_PrevaleSullaTipologia()
    {
        var filtro = new FiltroScadenzario(
            TipoCliente:  TipoAnagrafica.Privato,
            IdAnagrafica: Guid.NewGuid());

        var testo = ScadenzarioPdfService.ComponiDescrizioneFiltro(filtro, "ROSSI MARIO", null);

        Assert.StartsWith("Cliente: ROSSI MARIO", testo);
        Assert.DoesNotContain("Privati", testo);
    }

    [Theory]
    [InlineData(TipoAnagrafica.Societa,      "Clienti: Società")]
    [InlineData(TipoAnagrafica.Privato,      "Clienti: Privati")]
    [InlineData(TipoAnagrafica.EntePubblico, "Clienti: Enti pubblici")]
    public void ComponiDescrizioneFiltro_TipologiaCliente(TipoAnagrafica tipo, string atteso)
    {
        var testo = ScadenzarioPdfService.ComponiDescrizioneFiltro(
            new FiltroScadenzario(TipoCliente: tipo), null, null);

        Assert.StartsWith(atteso, testo);
    }

    [Fact]
    public void ComponiDescrizioneFiltro_TuttiICriteri_ComponeTutteLeParti()
    {
        var filtro = new FiltroScadenzario(
            IdAnagrafica:   Guid.NewGuid(),
            IdTipoAttivita: Guid.NewGuid(),
            DallaData:      new DateOnly(2026, 1, 1),
            AllaData:       new DateOnly(2026, 12, 31),
            Scadute:        FiltroScadute.SoloNonScadute,
            Evase:          FiltroEvase.SoloNonEvase);

        var testo = ScadenzarioPdfService.ComponiDescrizioneFiltro(filtro, "GARDACAMP S.R.L.", "PROGETTAZIONI");

        Assert.Equal(
            "Cliente: GARDACAMP S.R.L. - Attività: PROGETTAZIONI - " +
            "Scadenze dal 01/01/2026 al 31/12/2026 - Solo non scadute - Solo non evase",
            testo);
    }

    [Fact]
    public void ComponiDescrizioneFiltro_SoloDataInizio_ScadenzeDal()
    {
        var testo = ScadenzarioPdfService.ComponiDescrizioneFiltro(
            new FiltroScadenzario(DallaData: new DateOnly(2026, 7, 1)), null, null);

        Assert.Contains("Scadenze dal 01/07/2026", testo);
        Assert.DoesNotContain(" al ", testo.Replace("Scadenze dal", ""));
    }

    [Fact]
    public void ComponiDescrizioneFiltro_SoloDataFine_ScadenzeFinoAl()
    {
        var testo = ScadenzarioPdfService.ComponiDescrizioneFiltro(
            new FiltroScadenzario(AllaData: new DateOnly(2026, 12, 31)), null, null);

        Assert.Contains("Scadenze fino al 31/12/2026", testo);
    }

    [Fact]
    public void ComponiDescrizioneFiltro_SoloScaduteESoloEvase()
    {
        var testo = ScadenzarioPdfService.ComponiDescrizioneFiltro(
            new FiltroScadenzario(Scadute: FiltroScadute.SoloScadute, Evase: FiltroEvase.SoloEvase),
            null, null);

        Assert.EndsWith("Solo scadute - Solo evase", testo);
    }

    // ── Risoluzione nomi per il piè di pagina ────────────────────────────────

    [Fact]
    public async Task GeneraAsync_ConClienteETipoSelezionati_RisolveINomiSenzaErrori()
    {
        var (sut, _, anagrafiche, tipi) = NewSut();
        var idCliente = Guid.NewGuid();
        var idTipo    = Guid.NewGuid();
        anagrafiche.Cliente = new Anagrafica
        {
            IdAnagrafica   = idCliente,
            TipoAnagrafica = TipoAnagrafica.Societa,
            RagioneSociale = "GARDACAMP S.R.L.",
        };
        tipi.Tipo = new TipoAttivita
        {
            IdTipoAttivita = idTipo,
            Descrizione    = "PROGETTAZIONI",
            GestisciCome   = GestisciCome.Progetto,
        };

        var pdf = await sut.GeneraAsync(new FiltroScadenzario(
            IdAnagrafica: idCliente, IdTipoAttivita: idTipo));

        AssertIsPdf(pdf);
    }
}
