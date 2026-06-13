using ICMFatturazioni.Web.Validation;

namespace ICMFatturazioni.Tests.Validation;

/// <summary>
/// Test dell'helper <see cref="BancariValidazione"/>: formato/checksum IBAN,
/// estrazione ABI/CAB, formato ABI/CAB. Fixture IBAN: l'esempio canonico
/// italiano IT60X0542811101000000123456 (ABI 05428, CAB 11101).
/// </summary>
public class BancariValidazioneTests
{
    private const string IbanValido = "IT60X0542811101000000123456";

    [Fact]
    public void NormalizzaIban_RimuoveSpaziEMaiuscola()
    {
        Assert.Equal(IbanValido, BancariValidazione.NormalizzaIban(" it60 x054 2811 1010 0000 0123 456 "));
    }

    [Fact]
    public void IbanValido_Canonico_True()
    {
        Assert.True(BancariValidazione.IbanValido(IbanValido));
    }

    [Fact]
    public void IbanValido_ChecksumErrato_False()
    {
        // Cifre di controllo alterate (60 → 99): il MOD-97 deve fallire.
        Assert.False(BancariValidazione.IbanValido("IT99X0542811101000000123456"));
    }

    [Fact]
    public void IbanBenFormato_LunghezzaErrataPerItalia_False()
    {
        // IT con lunghezza diversa da 27.
        Assert.False(BancariValidazione.IbanBenFormato("IT60X05428111010000001234"));
    }

    [Fact]
    public void IbanBenFormato_CaratteriNonAlfanumerici_False()
    {
        Assert.False(BancariValidazione.IbanBenFormato("IT60X05428-1101000000123456"));
    }

    [Fact]
    public void TryEstraiAbiCab_IbanItaliano_EstraeAbiECab()
    {
        var ok = BancariValidazione.TryEstraiAbiCab(IbanValido, out var abi, out var cab);
        Assert.True(ok);
        Assert.Equal("05428", abi);
        Assert.Equal("11101", cab);
    }

    [Fact]
    public void TryEstraiAbiCab_IbanNonItaliano_False()
    {
        // IBAN tedesco (lunghezza 22): niente estrazione ABI/CAB all'italiana.
        var ok = BancariValidazione.TryEstraiAbiCab("DE89370400440532013000", out _, out _);
        Assert.False(ok);
    }

    [Theory]
    [InlineData("05428", true)]
    [InlineData("00001", true)]
    [InlineData("123", false)]
    [InlineData("123456", false)]
    [InlineData("1234A", false)]
    [InlineData(null, true)]   // assente = ok (presenza è del chiamante)
    [InlineData("   ", true)]
    public void CodiceAbiCabFormatoValido_VerificaCinqueCifre(string? codice, bool atteso)
    {
        Assert.Equal(atteso, BancariValidazione.CodiceAbiCabFormatoValido(codice));
    }
}
