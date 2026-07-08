using ICMFatturazioni.Web.Managers;
using ICMFatturazioni.Web.Models;
using ICMFatturazioni.Web.Services;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test del servizio di coerenza numero/data fattura: la numerazione deve seguire
/// l'ordine cronologico (numero crescente ⟹ data non decrescente). Servizio puro.
/// </summary>
public class FatturaCoerenzaValidatorTests
{
    private readonly FatturaCoerenzaValidator _sut = new();

    private static FatturaNumeroData F(int numero, int anno, int mese, int giorno)
        => new(numero, new DateOnly(anno, mese, giorno));

    [Fact]
    public void AnnoVuoto_NessunControllo_Passa()
    {
        _sut.Verifica(1, new DateOnly(2026, 3, 10), Array.Empty<FatturaNumeroData>());
    }

    [Fact]
    public void InCoda_DataUgualeOSuccessiva_Passa()
    {
        var anno = new[] { F(1, 2026, 1, 10), F(2, 2026, 2, 20) };
        _sut.Verifica(3, new DateOnly(2026, 3, 1), anno);   // numero e data entrambi maggiori
    }

    [Fact]
    public void NumeroDuplicato_Lancia()
    {
        var anno = new[] { F(1, 2026, 1, 10), F(2, 2026, 2, 20) };
        var ex = Assert.Throws<FatturaInvalidaException>(
            () => _sut.Verifica(2, new DateOnly(2026, 2, 25), anno));
        Assert.Equal(FatturaMotivoInvalido.NumeroDuplicato, ex.Motivo);
    }

    [Fact]
    public void DataAnterioreAllaPrecedente_Lancia()
    {
        var anno = new[] { F(1, 2026, 1, 10), F(2, 2026, 2, 20) };
        // Numero 3 (in coda) ma data anteriore alla n.2 → incoerente.
        var ex = Assert.Throws<FatturaInvalidaException>(
            () => _sut.Verifica(3, new DateOnly(2026, 2, 1), anno));
        Assert.Equal(FatturaMotivoInvalido.SequenzaDataNumeroIncoerente, ex.Motivo);
    }

    [Fact]
    public void DataPosterioreAllaSuccessiva_Lancia()
    {
        var anno = new[] { F(1, 2026, 1, 10), F(3, 2026, 3, 20) };
        // Si inserisce il numero 2 (in mezzo) con data DOPO la n.3 → incoerente.
        var ex = Assert.Throws<FatturaInvalidaException>(
            () => _sut.Verifica(2, new DateOnly(2026, 4, 1), anno));
        Assert.Equal(FatturaMotivoInvalido.SequenzaDataNumeroIncoerente, ex.Motivo);
    }

    [Fact]
    public void InMezzo_DataCoerenteConVicine_Passa()
    {
        var anno = new[] { F(1, 2026, 1, 10), F(3, 2026, 3, 20) };
        // Numero 2 con data fra la n.1 e la n.3 → coerente.
        _sut.Verifica(2, new DateOnly(2026, 2, 15), anno);
    }

    [Fact]
    public void DataUgualeAllaVicina_Passa()
    {
        var anno = new[] { F(1, 2026, 5, 10) };
        _sut.Verifica(2, new DateOnly(2026, 5, 10), anno);   // stessa data, numero maggiore: ammesso
    }
}
