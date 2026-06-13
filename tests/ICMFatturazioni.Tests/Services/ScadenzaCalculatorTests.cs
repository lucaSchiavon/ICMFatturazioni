using ICMFatturazioni.Web.Services;

namespace ICMFatturazioni.Tests.Services;

/// <summary>
/// Test del calcolo scadenze (dispensa cap. 4, §5.1). Replica 1:1 gli esempi
/// numerici della dispensa. Anno 2025 (non bisestile).
/// </summary>
public class ScadenzaCalculatorTests
{
    private readonly ScadenzaCalculator _sut = new();

    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    [Fact]
    public void FineMeseBase_8Giu_30gg_FineMese_31Lug()
    {
        var rate = _sut.Calcola(D(2025, 6, 8), 1, new[] { 30 }, fineMese: true, ggPiu: null, importo: 100m);

        var r = Assert.Single(rate);
        Assert.Equal(D(2025, 7, 31), r.DataScadenza);
    }

    [Fact]
    public void GiorniAggiuntivi_8Giu_30gg_FineMese_Piu10_10Ago()
    {
        var rate = _sut.Calcola(D(2025, 6, 8), 1, new[] { 30 }, fineMese: true, ggPiu: 10, importo: 100m);

        Assert.Equal(D(2025, 8, 10), rate[0].DataScadenza);
    }

    [Fact]
    public void Rateazione_1Lug_30_60_90_FineMese_9000_TreRateDa3000()
    {
        var rate = _sut.Calcola(D(2025, 7, 1), 3, new[] { 30, 60, 90 }, fineMese: true, ggPiu: null, importo: 9000m);

        Assert.Collection(rate,
            r => { Assert.Equal(D(2025, 7, 31), r.DataScadenza); Assert.Equal(3000m, r.Importo); },
            r => { Assert.Equal(D(2025, 8, 31), r.DataScadenza); Assert.Equal(3000m, r.Importo); },
            r => { Assert.Equal(D(2025, 9, 30), r.DataScadenza); Assert.Equal(3000m, r.Importo); });
        Assert.Equal(9000m, rate.Sum(r => r.Importo));
    }

    [Fact]
    public void TestFig5_18Giu_60gg_FineMese_31Ago()
    {
        var rate = _sut.Calcola(D(2025, 6, 18), 1, new[] { 60 }, fineMese: true, ggPiu: null, importo: 100m);

        Assert.Equal(D(2025, 8, 31), rate[0].DataScadenza);
    }

    [Fact]
    public void SenzaFineMese_NessunoSpostamento()
    {
        // 8 giu + 30 gg = 8 lug, senza spostamento a fine mese.
        var rate = _sut.Calcola(D(2025, 6, 8), 1, new[] { 30 }, fineMese: false, ggPiu: null, importo: 100m);
        Assert.Equal(D(2025, 7, 8), rate[0].DataScadenza);
    }

    [Fact]
    public void GiorniAggiuntivi_IgnoratiSenzaFineMese()
    {
        // ggPiu ha effetto solo dopo il fine mese: senza f.m. va ignorato.
        var rate = _sut.Calcola(D(2025, 6, 8), 1, new[] { 30 }, fineMese: false, ggPiu: 10, importo: 100m);
        Assert.Equal(D(2025, 7, 8), rate[0].DataScadenza);
    }

    [Fact]
    public void RipartizioneImporto_UltimaRataAssorbeIlResto()
    {
        // 100 / 3 = 33,33 + 33,33 + 33,34 (Σ = 100).
        var rate = _sut.Calcola(D(2025, 1, 1), 3, new[] { 0, 0, 0 }, fineMese: false, ggPiu: null, importo: 100m);

        Assert.Equal(33.33m, rate[0].Importo);
        Assert.Equal(33.33m, rate[1].Importo);
        Assert.Equal(33.34m, rate[2].Importo);
        Assert.Equal(100m, rate.Sum(r => r.Importo));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void NumScadenzeFuoriRange_Lancia(int num)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _sut.Calcola(D(2025, 1, 1), num, new[] { 0, 0, 0, 0 }, false, null, 100m));
    }
}
