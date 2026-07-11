using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Models;
using ICMFatturazioni.Web.Services;

namespace ICMFatturazioni.Tests.Services;

/// <summary>
/// Test del report "Riepilogo attività consulente": filtro righe (D-C1 solo
/// carico Studio, stato D-C4, raffinamenti), descrizione filtro per il piè di
/// pagina e smoke test del rendering (documento valido con i totali del
/// campione legacy: 5.500 · 2.000 · 3.500).
/// </summary>
public class RiepilogoConsulentePdfServiceTests
{
    private static SchedaConsulenzaRiga Riga(
        decimal importo,
        decimal pagato = 0,
        CaricoConsulenza carico = CaricoConsulenza.Studio,
        string cliente = "3 ESSE S.A.S.",
        string numero = "123",
        Guid? idAnagrafica = null,
        Guid? idAttivita = null,
        string consulente = "LUCA SCHIAVON") => new()
    {
        IdAttivitaConsulente  = Guid.NewGuid(),
        IdAnagrafica          = idAnagrafica ?? Guid.NewGuid(),
        IdAttivita            = idAttivita ?? Guid.NewGuid(),
        ConsulenteDescrizione = consulente,
        RagioneSociale        = cliente,
        AttivitaNumero        = numero,
        AttivitaDescrizione   = "Attività di test",
        TipoDescrizione       = "SVILUPPO SOFTWARE PERSON",
        Carico                = carico,
        Importo               = importo,
        Pagato                = pagato,
    };

    // ─── FiltraRighe ─────────────────────────────────────────────────────

    [Fact]
    public void FiltraRighe_EscludeSempreIlCaricoCliente_DC1()
    {
        // Scenario del campione: 4.000 S + 1.500 S + 1.000 C → nel report solo le S.
        var righe = new[]
        {
            Riga(4000m),
            Riga(1500m, cliente: "ALLEGRI FEDERICA", numero: "897"),
            Riga(1000m, carico: CaricoConsulenza.Cliente),
        };

        var filtrate = RiepilogoConsulentePdfService.FiltraRighe(
            righe, new FiltroRiepilogoConsulente(null, Stato: FiltroStatoConsulenze.Tutte));

        Assert.Equal(2, filtrate.Count);
        Assert.Equal(5500m, filtrate.Sum(r => r.Importo));
        Assert.DoesNotContain(filtrate, r => r.Carico == CaricoConsulenza.Cliente);
    }

    [Fact]
    public void FiltraRighe_StatoAperte_EscludeLeSaldate()
    {
        var righe = new[]
        {
            Riga(4000m, pagato: 2000m),   // aperta
            Riga(1500m, pagato: 1500m),   // saldata
        };

        var aperte = RiepilogoConsulentePdfService.FiltraRighe(
            righe, new FiltroRiepilogoConsulente(null, Stato: FiltroStatoConsulenze.Aperte));
        var chiuse = RiepilogoConsulentePdfService.FiltraRighe(
            righe, new FiltroRiepilogoConsulente(null, Stato: FiltroStatoConsulenze.Chiuse));

        Assert.Equal(4000m, Assert.Single(aperte).Importo);
        Assert.Equal(1500m, Assert.Single(chiuse).Importo);
    }

    [Fact]
    public void FiltraRighe_RaffinamentiClienteEAttivita()
    {
        var idAnagrafica = Guid.NewGuid();
        var idAttivita = Guid.NewGuid();
        var righe = new[]
        {
            Riga(4000m, idAnagrafica: idAnagrafica, idAttivita: idAttivita),
            Riga(1500m),   // altro cliente/attività
        };

        var perCliente = RiepilogoConsulentePdfService.FiltraRighe(
            righe, new FiltroRiepilogoConsulente(null, IdAnagrafica: idAnagrafica, Stato: FiltroStatoConsulenze.Tutte));
        var perAttivita = RiepilogoConsulentePdfService.FiltraRighe(
            righe, new FiltroRiepilogoConsulente(null, IdAttivita: idAttivita, Stato: FiltroStatoConsulenze.Tutte));

        Assert.Equal(4000m, Assert.Single(perCliente).Importo);
        Assert.Equal(4000m, Assert.Single(perAttivita).Importo);
    }

    // ─── ComponiDescrizioneFiltro ────────────────────────────────────────

    [Fact]
    public void ComponiDescrizioneFiltro_CasoBase_ComeIlLegacy()
    {
        var testo = RiepilogoConsulentePdfService.ComponiDescrizioneFiltro(
            new FiltroRiepilogoConsulente(null), nomeCliente: null, nomeAttivita: null);
        Assert.Equal("Tutti i Clienti - Tutte le attività - Solo consulenze aperte", testo);
    }

    [Fact]
    public void ComponiDescrizioneFiltro_ConRaffinamenti()
    {
        var testo = RiepilogoConsulentePdfService.ComponiDescrizioneFiltro(
            new FiltroRiepilogoConsulente(null,
                IdAnagrafica: Guid.NewGuid(), IdAttivita: Guid.NewGuid(),
                Stato: FiltroStatoConsulenze.Tutte),
            nomeCliente: "3 ESSE S.A.S.", nomeAttivita: "123-Attività di test");
        Assert.Equal("Cliente: 3 ESSE S.A.S. - Attività: 123-Attività di test - Tutte le consulenze", testo);
    }

    // ─── Rendering (smoke test) ──────────────────────────────────────────

    [Fact]
    public void Render_ScenarioDelCampione_ProduceUnPdf()
    {
        // Replica del campione legacy: 4.000 con due tranche da 1.000 + 1.500
        // senza tranche → totale generale 5.500 · 2.000 · 3.500.
        var riga1 = Riga(4000m, pagato: 2000m);
        var riga2 = Riga(1500m, cliente: "ALLEGRI FEDERICA", numero: "897");
        var tranche = new Dictionary<Guid, IReadOnlyList<AttivitaConsulentePagamento>>
        {
            [riga1.IdAttivitaConsulente] = new[]
            {
                new AttivitaConsulentePagamento { IdAttivitaConsulente = riga1.IdAttivitaConsulente, DataPagamento = new DateOnly(2026, 7, 10), Importo = 1000m, Nota = "PRIMO PAGAMENTO" },
                new AttivitaConsulentePagamento { IdAttivitaConsulente = riga1.IdAttivitaConsulente, DataPagamento = new DateOnly(2026, 7, 10), Importo = 1000m, Nota = "SECONDO" },
            },
        };
        var data = new RiepilogoConsulentePdfData(
            Righe:             new[] { riga1, riga2 },
            TranchePerRiga:    tranche,
            NomeConsulente:    "LUCA SCHIAVON",
            DescrizioneFiltro: "Tutti i Clienti - Tutte le attività - Solo consulenze aperte",
            GeneratoIl:        new DateTime(2026, 7, 10, 12, 11, 22));

        var pdf = new RiepilogoConsulentePdfDocument(data).Render();

        Assert.True(pdf.Length > 1000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void Render_VarianteGenerale_SenzaRighe_ProduceUnPdf()
    {
        var data = new RiepilogoConsulentePdfData(
            Righe:             Array.Empty<SchedaConsulenzaRiga>(),
            TranchePerRiga:    new Dictionary<Guid, IReadOnlyList<AttivitaConsulentePagamento>>(),
            NomeConsulente:    null,   // variante generale
            DescrizioneFiltro: "Tutti i Clienti - Tutte le attività - Tutte le consulenze",
            GeneratoIl:        new DateTime(2026, 7, 11, 10, 0, 0));

        var pdf = new RiepilogoConsulentePdfDocument(data).Render();

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }
}
