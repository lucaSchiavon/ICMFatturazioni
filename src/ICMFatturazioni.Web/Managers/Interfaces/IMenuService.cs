using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Web.Managers.Interfaces;

/// <summary>
/// Servizio che calcola, per l'UTENTE CORRENTE, il menu visibile e l'insieme
/// delle pagine a cui può accedere. È il punto unico dell'autorizzazione
/// "dinamica": il NavMenu lo usa per disegnarsi, la guardia di rotta per
/// consentire/negare l'accesso a una pagina. Vedi memoria menu-ruoli-dinamici.
/// </summary>
public interface IMenuService
{
    /// <summary>
    /// Albero di menu già filtrato per l'utente corrente (gruppi con le sole
    /// sottovoci visibili). Pronto per il rendering del NavMenu.
    /// </summary>
    Task<IReadOnlyList<MenuNodo>> GetMenuVisibileAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// True se l'utente corrente può accedere alla pagina Razor indicata
    /// (nome classe, es. "Anagrafiche"). Le pagine di sistema (Home, Dashboard,
    /// Login, errori) sono sempre consentite; Superadmin vede tutto; Admin vede
    /// tutto tranne le pagine Superadmin-only; gli altri ruoli solo le pagine
    /// dei menu loro assegnati.
    /// </summary>
    Task<bool> PuoAccedereAsync(string pageName, CancellationToken cancellationToken = default);
}
