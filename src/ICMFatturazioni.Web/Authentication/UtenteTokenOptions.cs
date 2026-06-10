namespace ICMFatturazioni.Web.Authentication;

/// <summary>
/// Configurazione dei magic-link utente (attivazione/reset, migration 013).
/// Bindata dalla sezione <c>UtenteToken</c> di appsettings.json.
/// </summary>
public sealed class UtenteTokenOptions
{
    public const string SectionName = "UtenteToken";

    /// <summary>
    /// Validità del link di attivazione (primo accesso). Default 7 giorni:
    /// l'utente invitato può tardare a leggere l'email.
    /// </summary>
    public int AttivazioneOreDefault { get; set; } = 168;

    /// <summary>
    /// Validità del link di reset password. Default 1 ora: più stretto, è un
    /// recupero che ci si aspetta venga completato subito.
    /// </summary>
    public int ResetOreDefault { get; set; } = 1;
}
