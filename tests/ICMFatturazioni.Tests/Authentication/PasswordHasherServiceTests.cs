using ICMFatturazioni.Web.Authentication;

namespace ICMFatturazioni.Tests.Authentication;

/// <summary>
/// Test del servizio di hashing password (PBKDF2 v3 del framework). Verifica:
///   1) il round-trip hash → verify riconosce la password corretta;
///   2) password errata o input vuoti falliscono in modo sicuro;
///   3) l'hash non è la password in chiaro ed è "salato" (due hash della stessa
///      password differiscono).
/// </summary>
public class PasswordHasherServiceTests
{
    private static PasswordHasherService NewSut() => new();

    [Fact]
    public void HashPassword_PasswordVuota_LanciaArgumentException()
    {
        var sut = NewSut();
        Assert.Throws<ArgumentException>(() => sut.HashPassword(""));
    }

    [Fact]
    public void HashPassword_PoiVerify_RiconosceLaPasswordCorretta()
    {
        var sut = NewSut();
        var hash = sut.HashPassword("S3gr3t0!");

        Assert.True(sut.VerifyHashedPassword(hash, "S3gr3t0!"));
    }

    [Fact]
    public void VerifyHashedPassword_PasswordErrata_RestituisceFalse()
    {
        var sut = NewSut();
        var hash = sut.HashPassword("S3gr3t0!");

        Assert.False(sut.VerifyHashedPassword(hash, "password-sbagliata"));
    }

    [Theory]
    [InlineData("", "qualcosa")]
    [InlineData("hash-finto", "")]
    [InlineData("", "")]
    public void VerifyHashedPassword_InputVuoti_RestituisceFalseSenzaEccezioni(string hash, string password)
    {
        var sut = NewSut();
        Assert.False(sut.VerifyHashedPassword(hash, password));
    }

    [Fact]
    public void HashPassword_NonRestituisceLaPasswordInChiaroEdESalata()
    {
        var sut = NewSut();
        var h1 = sut.HashPassword("S3gr3t0!");
        var h2 = sut.HashPassword("S3gr3t0!");

        // L'hash non coincide con la password e, grazie al salt casuale, due
        // hash della stessa password sono diversi tra loro.
        Assert.NotEqual("S3gr3t0!", h1);
        Assert.NotEqual(h1, h2);
    }
}
