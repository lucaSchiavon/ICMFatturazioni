namespace ICMFatturazioni.Web.Authentication;

/// <summary>
/// Configurazione del seed dell'utente Admin iniziale (sezione <c>Admin</c>
/// di appsettings / user-secrets / variabili d'ambiente).
/// </summary>
/// <remarks>
/// Lo username può stare in appsettings (non sensibile). La <b>password</b>
/// deve arrivare da user-secrets (dev) o variabile d'ambiente (prod):
///   dev:  dotnet user-secrets set "Admin:DefaultPassword" "..." --project src/ICMFatturazioni.Web
///   prod: variabile d'ambiente Admin__DefaultPassword
/// Se la password è vuota, il seeder salta la creazione.
/// </remarks>
public sealed class AdminSeederOptions
{
    public const string SectionName = "Admin";

    public string DefaultUsername { get; set; } = "admin";
    public string? DefaultPassword { get; set; }
    public string? DefaultEmail { get; set; }
}
