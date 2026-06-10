namespace ICMFatturazioni.Web.Authentication;

/// <summary>
/// Configurazione del seed dell'utente Superadmin (sezione <c>Superadmin</c>).
/// </summary>
/// <remarks>
/// In sviluppo di norma la password resta vuota → nessun Superadmin creato.
/// In produzione il Superadmin si crea SOLO valorizzando
/// <c>Superadmin__Password</c> via variabile d'ambiente (mai in chiaro su
/// appsettings versionato). Idempotente: se l'utente esiste già non lo ricrea.
/// </remarks>
public sealed class SuperadminSeederOptions
{
    public const string SectionName = "Superadmin";

    public string Username { get; set; } = "superadmin";
    public string? Password { get; set; }
    public string? Email { get; set; }
}
