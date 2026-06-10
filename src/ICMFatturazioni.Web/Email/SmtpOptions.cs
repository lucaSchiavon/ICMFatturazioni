namespace ICMFatturazioni.Web.Email;

/// <summary>
/// Configurazione SMTP per l'invio email (Brevo o qualunque relay SMTP).
/// Bindata dalla sezione <c>Smtp</c> di appsettings.json.
/// </summary>
/// <remarks>
/// SEGRETI: <see cref="Password"/> (la SMTP key) va in user-secrets / variabile
/// d'ambiente (<c>Smtp__Password</c>), MAI in appsettings versionato (CLAUDE.md
/// "Gestione di segreti e configurazioni"). Brevo: Host = smtp-relay.brevo.com,
/// Port = 587, UseStartTls = true, User = login SMTP, Password = SMTP key,
/// FromAddress = mittente verificato.
/// </remarks>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public string? User { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "ICM Fatturazioni";
    public bool UseStartTls { get; set; } = true;

    /// <summary>
    /// Se <c>Host</c> non è configurato si usa il <see cref="LogEmailSender"/>
    /// di sviluppo (le email non partono, il link finisce nel log). Selezione
    /// fatta in Program.cs.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);
}
