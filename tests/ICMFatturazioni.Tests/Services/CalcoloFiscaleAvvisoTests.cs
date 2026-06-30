using ICMFatturazioni.Web.Services;

namespace ICMFatturazioni.Tests.Services;

/// <summary>
/// Test della cascata di calcolo fiscale dell'avviso (dispensa cap. 7).
/// I due casi "canonici" replicano gli esempi numerici dei documenti.
/// </summary>
public class CalcoloFiscaleAvvisoTests
{
    private readonly CalcoloFiscaleAvviso _sut = new();

    // -------------------------------------------------------------------------
    // Esempi canonici dei documenti
    // -------------------------------------------------------------------------

    [Fact]
    public void Calcola_EsempioDispensa_Imponibile8000_ImpresaConSpese300()
    {
        // Dispensa cap. 7: imponibile 8.000, cassa 4%, IVA 22%, ritenuta 20%, spese 300.
        var r = _sut.Calcola(new CalcoloFiscaleInput(
            Imponibile:       8000m,
            AliquotaCassa:    4m,
            AliquotaIva:      22m,
            AliquotaRitenuta: 20m,
            ApplicaCassa:     true,
            ApplicaRitenuta:  true,
            SpeseArt15:       300m));

        Assert.Equal(8000.00m, r.Imponibile);
        Assert.Equal( 320.00m, r.Cassa);
        Assert.Equal(8320.00m, r.ImponibilePiuCassa);
        Assert.Equal(1830.40m, r.Iva);
        Assert.Equal(10150.40m, r.Totale);
        Assert.Equal(1600.00m, r.Ritenuta);
        Assert.Equal( 300.00m, r.SpeseArt15);
        Assert.Equal(8850.40m, r.TotaleNostroAvere);
    }

    [Fact]
    public void Calcola_EsempioReport_Imponibile2500_ImpresaConSpese250()
    {
        // Report "Avviso di parcella": imponibile 2.500, cassa 100, IVA 572 su 2.600,
        // totale 3.172, ritenuta 500, spese 250 → totale nostro avere 2.922.
        var r = _sut.Calcola(new CalcoloFiscaleInput(
            Imponibile:       2500m,
            AliquotaCassa:    4m,
            AliquotaIva:      22m,
            AliquotaRitenuta: 20m,
            ApplicaCassa:     true,
            ApplicaRitenuta:  true,
            SpeseArt15:       250m));

        Assert.Equal( 100.00m, r.Cassa);
        Assert.Equal(2600.00m, r.ImponibilePiuCassa);
        Assert.Equal( 572.00m, r.Iva);
        Assert.Equal(3172.00m, r.Totale);
        Assert.Equal( 500.00m, r.Ritenuta);
        Assert.Equal(2922.00m, r.TotaleNostroAvere);
    }

    // -------------------------------------------------------------------------
    // Condizione sul soggetto: ritenuta solo per imprese/sostituti d'imposta
    // -------------------------------------------------------------------------

    [Fact]
    public void Calcola_Privato_NienteRitenuta()
    {
        // Stesso imponibile ma cliente privato (non sostituto): ritenuta = 0.
        var r = _sut.Calcola(new CalcoloFiscaleInput(
            Imponibile:       2500m,
            AliquotaCassa:    4m,
            AliquotaIva:      22m,
            AliquotaRitenuta: 20m,
            ApplicaCassa:     true,
            ApplicaRitenuta:  false,
            SpeseArt15:       0m));

        Assert.Equal(0m, r.Ritenuta);
        // Totale nostro avere = totale (3.172) − 0 + 0.
        Assert.Equal(3172.00m, r.TotaleNostroAvere);
    }

    // -------------------------------------------------------------------------
    // Varianti
    // -------------------------------------------------------------------------

    [Fact]
    public void Calcola_SenzaCassa_IvaSuSoloImponibile()
    {
        var r = _sut.Calcola(new CalcoloFiscaleInput(
            Imponibile:       1000m,
            AliquotaCassa:    4m,
            AliquotaIva:      22m,
            AliquotaRitenuta: 20m,
            ApplicaCassa:     false,
            ApplicaRitenuta:  true,
            SpeseArt15:       0m));

        Assert.Equal(0m, r.Cassa);
        Assert.Equal(1000.00m, r.ImponibilePiuCassa);
        Assert.Equal(220.00m, r.Iva);          // 22% di 1000
        Assert.Equal(1220.00m, r.Totale);
        Assert.Equal(200.00m, r.Ritenuta);     // 20% di 1000
        Assert.Equal(1020.00m, r.TotaleNostroAvere);
    }

    [Fact]
    public void Calcola_IvaZero_Esente_IvaNulla()
    {
        var r = _sut.Calcola(new CalcoloFiscaleInput(
            Imponibile:       1000m,
            AliquotaCassa:    4m,
            AliquotaIva:      0m,
            AliquotaRitenuta: 20m,
            ApplicaCassa:     true,
            ApplicaRitenuta:  true,
            SpeseArt15:       0m));

        Assert.Equal(40.00m, r.Cassa);
        Assert.Equal(0m, r.Iva);
        Assert.Equal(1040.00m, r.Totale);
        Assert.Equal(200.00m, r.Ritenuta);
        Assert.Equal(840.00m, r.TotaleNostroAvere);
    }

    [Fact]
    public void Calcola_Arrotondamento_DueDecimali()
    {
        // Imponibile che genera decimali non esatti: 333,33 × 4% = 13,3332 → 13,33.
        var r = _sut.Calcola(new CalcoloFiscaleInput(
            Imponibile:       333.33m,
            AliquotaCassa:    4m,
            AliquotaIva:      22m,
            AliquotaRitenuta: 20m,
            ApplicaCassa:     true,
            ApplicaRitenuta:  true,
            SpeseArt15:       0m));

        Assert.Equal(13.33m, r.Cassa);                 // 13,3332 → 13,33
        Assert.Equal(346.66m, r.ImponibilePiuCassa);   // 333,33 + 13,33
        Assert.Equal(76.27m, r.Iva);                   // 346,66 × 22% = 76,2652 → 76,27
        Assert.Equal(66.67m, r.Ritenuta);              // 333,33 × 20% = 66,666 → 66,67
    }

    [Fact]
    public void Calcola_SpeseArt15_FuoriBaseIva()
    {
        // Le spese non toccano imponibile/cassa/IVA: si sommano solo alla fine.
        var senza = _sut.Calcola(new CalcoloFiscaleInput(1000m, 4m, 22m, 20m, true, true, 0m));
        var con   = _sut.Calcola(new CalcoloFiscaleInput(1000m, 4m, 22m, 20m, true, true, 150m));

        Assert.Equal(senza.Iva, con.Iva);                                  // IVA invariata
        Assert.Equal(senza.Totale, con.Totale);                            // Totale (ante spese) invariato
        Assert.Equal(senza.TotaleNostroAvere + 150m, con.TotaleNostroAvere); // +150 solo sul netto
    }

    // -------------------------------------------------------------------------
    // Guard
    // -------------------------------------------------------------------------

    [Fact]
    public void Calcola_ImponibileNegativo_Lancia()
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            _sut.Calcola(new CalcoloFiscaleInput(-1m, 4m, 22m, 20m, true, true, 0m)));

    [Fact]
    public void Calcola_AliquotaNegativa_Lancia()
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            _sut.Calcola(new CalcoloFiscaleInput(1000m, -4m, 22m, 20m, true, true, 0m)));

    [Fact]
    public void Calcola_SpeseNegative_Lancia()
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            _sut.Calcola(new CalcoloFiscaleInput(1000m, 4m, 22m, 20m, true, true, -1m)));
}
