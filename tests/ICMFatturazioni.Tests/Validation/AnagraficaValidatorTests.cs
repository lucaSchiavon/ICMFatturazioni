using ICMFatturazioni.Web.Validation;

namespace ICMFatturazioni.Tests.Validation;

/// <summary>
/// Test dell'helper <see cref="AnagraficaValidator"/>: email, PEC, partita IVA
/// (Luhn), codice fiscale persona/azienda, campo unico P.IVA/CF e codice
/// destinatario SDI. Fixture: P.IVA valida canonica 12345670785 (esempio AdE);
/// CF persona valido RSSMRA85M01H501Q (carattere di controllo calcolato).
/// </summary>
public class AnagraficaValidatorTests
{
    private const string PivaValida = "12345670785";
    private const string CfPersonaValido = "RSSMRA85M01H501Q";

    // ---- Email --------------------------------------------------------------

    [Theory]
    [InlineData("mario.rossi@example.com")]
    [InlineData("a@b.it")]
    public void ValidaEmail_FormatoCorretto_Null(string email)
        => Assert.Null(AnagraficaValidator.ValidaEmail(email));

    [Theory]
    [InlineData("mario.rossi")]        // manca @dominio
    [InlineData("a@b")]                // dominio senza punto
    [InlineData("a b@example.com")]    // spazio interno
    public void ValidaEmail_FormatoErrato_Messaggio(string email)
        => Assert.NotNull(AnagraficaValidator.ValidaEmail(email));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidaEmail_Vuota_Null(string? email)
        => Assert.Null(AnagraficaValidator.ValidaEmail(email));

    // ---- PEC (stessa forma dell'email) --------------------------------------

    [Fact]
    public void ValidaPec_FormatoCorretto_Null()
        => Assert.Null(AnagraficaValidator.ValidaPec("azienda@pec.it"));

    [Fact]
    public void ValidaPec_FormatoErrato_Messaggio()
        => Assert.NotNull(AnagraficaValidator.ValidaPec("azienda@pec"));

    // ---- Partita IVA --------------------------------------------------------

    [Fact]
    public void PartitaIvaValida_Canonica_True()
        => Assert.True(AnagraficaValidator.PartitaIvaValida(PivaValida));

    [Theory]
    [InlineData("12345670786")]   // ultima cifra alterata → checksum errato
    [InlineData("1234567078")]    // 10 cifre
    [InlineData("123456707850")]  // 12 cifre
    [InlineData("1234567078A")]   // carattere non numerico
    public void PartitaIvaValida_Errata_False(string piva)
        => Assert.False(AnagraficaValidator.PartitaIvaValida(piva));

    [Fact]
    public void ValidaPartitaIva_Vuota_Null()
        => Assert.Null(AnagraficaValidator.ValidaPartitaIva(""));

    // ---- Codice fiscale persona ---------------------------------------------

    [Fact]
    public void CodiceFiscalePersonaValido_Canonico_True()
        => Assert.True(AnagraficaValidator.CodiceFiscalePersonaValido(CfPersonaValido));

    [Fact]
    public void CodiceFiscalePersonaValido_MinuscoloAmmesso_True()
        => Assert.True(AnagraficaValidator.CodiceFiscalePersonaValido(CfPersonaValido.ToLowerInvariant()));

    [Theory]
    [InlineData("RSSMRA85M01H501A")]  // carattere di controllo errato
    [InlineData("RSSMRA85M01H50")]    // lunghezza errata
    [InlineData("1SSMRA85M01H501Q")]  // prima posizione deve essere lettera
    public void CodiceFiscalePersonaValido_Errato_False(string cf)
        => Assert.False(AnagraficaValidator.CodiceFiscalePersonaValido(cf));

    // ---- Codice fiscale azienda (11 numerico oppure 16 persona) -------------

    [Theory]
    [InlineData("12345670785")]        // società: 11 cifre (formato P.IVA)
    [InlineData("RSSMRA85M01H501Q")]   // ditta individuale: 16 caratteri
    public void CodiceFiscaleAziendaValido_FormeAmmesse_True(string cf)
        => Assert.True(AnagraficaValidator.CodiceFiscaleAziendaValido(cf));

    [Theory]
    [InlineData("123456789")]          // né 11 né 16
    [InlineData("12345670786")]        // 11 ma checksum errato
    public void CodiceFiscaleAziendaValido_Errato_False(string cf)
        => Assert.False(AnagraficaValidator.CodiceFiscaleAziendaValido(cf));

    // ---- Campo unico P.IVA / Codice fiscale ---------------------------------

    [Theory]
    [InlineData("12345670785")]        // P.IVA
    [InlineData("RSSMRA85M01H501Q")]   // CF persona
    public void ValidaPivaOCodiceFiscale_FormeAmmesse_Null(string value)
        => Assert.Null(AnagraficaValidator.ValidaPivaOCodiceFiscale(value));

    [Fact]
    public void ValidaPivaOCodiceFiscale_Junk_Messaggio()
        => Assert.NotNull(AnagraficaValidator.ValidaPivaOCodiceFiscale("ABC123"));

    [Fact]
    public void ValidaPivaOCodiceFiscale_Vuoto_Null()
        => Assert.Null(AnagraficaValidator.ValidaPivaOCodiceFiscale(""));

    // ---- Codice destinatario SDI --------------------------------------------

    [Theory]
    [InlineData("ABC1234")]   // 7 alfanumerici (privati/B2B)
    [InlineData("0000000")]   // 7 zeri (destinatario via PEC)
    [InlineData("UFABCD")]    // 6 alfanumerici (PA)
    public void ValidaCodiceDestinatarioSdi_FormatoCorretto_Null(string codice)
        => Assert.Null(AnagraficaValidator.ValidaCodiceDestinatarioSdi(codice));

    [Theory]
    [InlineData("ABC12")]     // 5 caratteri
    [InlineData("ABC12345")]  // 8 caratteri
    [InlineData("ABC-123")]   // carattere non alfanumerico
    public void ValidaCodiceDestinatarioSdi_FormatoErrato_Messaggio(string codice)
        => Assert.NotNull(AnagraficaValidator.ValidaCodiceDestinatarioSdi(codice));

    [Fact]
    public void ValidaCodiceDestinatarioSdi_Vuoto_Null()
        => Assert.Null(AnagraficaValidator.ValidaCodiceDestinatarioSdi(""));

    // ---- CAP ----------------------------------------------------------------

    [Theory]
    [InlineData("37100")]
    [InlineData("00100")]
    public void ValidaCap_CinqueCifre_Null(string cap)
        => Assert.Null(AnagraficaValidator.ValidaCap(cap));

    [Theory]
    [InlineData("3710")]    // 4 cifre
    [InlineData("371000")]  // 6 cifre
    [InlineData("3710A")]   // carattere non numerico
    public void ValidaCap_FormatoErrato_Messaggio(string cap)
        => Assert.NotNull(AnagraficaValidator.ValidaCap(cap));

    [Fact]
    public void ValidaCap_Vuoto_Null()
        => Assert.Null(AnagraficaValidator.ValidaCap(""));
}
