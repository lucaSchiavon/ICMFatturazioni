namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Codici stabili dei ruoli di SISTEMA (seedati in migration 006, IsSistema=1).
/// Sono le uniche "costanti di ruolo" cablate nel codice: tutto il resto è
/// configurabile a runtime. Usati per:
///   - riconoscere i ruoli fissi indipendentemente dal nome visualizzato;
///   - popolare il claim di ruolo al login;
///   - applicare le regole di accesso fisse (Superadmin vede tutto incluso il
///     log errori; Admin vede tutto tranne il log errori).
/// </summary>
public static class RuoliSistema
{
    public const string Superadmin = "SUPERADMIN";
    public const string Admin = "ADMIN";
}
