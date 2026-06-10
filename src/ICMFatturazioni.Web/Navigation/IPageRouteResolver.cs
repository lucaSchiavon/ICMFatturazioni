namespace ICMFatturazioni.Web.Navigation;

/// <summary>
/// Risolve il nome di una classe/pagina Razor (es. "Anagrafiche") nella sua
/// rotta (es. "/anagrafiche"). Serve al NavMenu dinamico: le tabelle menu
/// memorizzano il NOME della pagina (chiave usata anche dalla guardia di
/// rotta, che confronta <c>routeData.PageType.Name</c>), mentre il link ha
/// bisogno della rotta effettiva dichiarata da <c>@page</c>.
/// </summary>
public interface IPageRouteResolver
{
    /// <summary>Rotta della pagina, o <c>null</c> se la pagina non esiste/non è routable.</summary>
    string? GetRoute(string pageClassName);
}
