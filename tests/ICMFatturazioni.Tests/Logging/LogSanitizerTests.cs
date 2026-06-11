using ICMFatturazioni.Web.Logging;

namespace ICMFatturazioni.Tests.Logging;

/// <summary>
/// Test del <c>LogSanitizer</c>: maschera credenziali e segreti, lasciando
/// invariato il testo innocuo e i valori null/vuoti.
/// </summary>
public class LogSanitizerTests
{
    [Theory]
    [InlineData("Login fallito; Password=SuperSegreta123; ok", "SuperSegreta123")]
    [InlineData("conn: Server=db01;Database=x", "db01")]
    [InlineData("header Token=abc.def.ghi", "abc.def.ghi")]
    [InlineData("AccessToken: zzz999", "zzz999")]
    [InlineData("Data Source=10.0.0.1;Initial Catalog=Fatt", "10.0.0.1")]
    public void Sanitize_MascheraIlSegreto(string input, string segreto)
    {
        var output = LogSanitizer.Sanitize(input);

        Assert.NotNull(output);
        Assert.DoesNotContain(segreto, output);
        Assert.Contains("***", output);
    }

    [Fact]
    public void Sanitize_PreservaIlTestoInnocuo()
    {
        const string input = "Operazione completata per l'utente bob alle 10:00.";

        Assert.Equal(input, LogSanitizer.Sanitize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Sanitize_NullOVuoto_PassaInvariato(string? input)
    {
        Assert.Equal(input, LogSanitizer.Sanitize(input));
    }
}
