namespace ICMFatturazioni.Web.Authentication;

/// <summary>
/// Nomi centralizzati delle policy di autorizzazione "fisse" (CLAUDE.md
/// Regola 5: le policy si dichiarano qui, non sparse nei componenti).
/// </summary>
/// <remarks>
/// Sono policy INCLUSIVE basate sui ruoli di SISTEMA:
///   - <see cref="RequireAdmin"/>: soddisfatta da Admin <b>o</b> Superadmin.
///   - <see cref="RequireSuperadmin"/>: solo Superadmin (es. log errori).
/// La visibilità delle altre pagine, per i ruoli custom, NON passa da qui ma
/// dal mapping dinamico ruolo↔menu (Tappa T2): vedi memoria menu-ruoli-dinamici.
/// </remarks>
public static class AuthorizationPolicies
{
    public const string RequireAdmin = "RequireAdmin";
    public const string RequireSuperadmin = "RequireSuperadmin";
}
