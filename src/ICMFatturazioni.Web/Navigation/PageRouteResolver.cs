using System.Reflection;
using ICMFatturazioni.Web.Components;
using Microsoft.AspNetCore.Components;

namespace ICMFatturazioni.Web.Navigation;

/// <summary>
/// Implementazione di <see cref="IPageRouteResolver"/>. All'avvio scansiona
/// una sola volta l'assembly per i componenti routable (con attributo
/// <see cref="RouteAttribute"/>) e costruisce la mappa nome-classe → rotta.
/// Registrato come singleton: la riflessione costa una volta sola.
/// </summary>
internal sealed class PageRouteResolver : IPageRouteResolver
{
    private readonly Dictionary<string, string> _routes;

    public PageRouteResolver()
    {
        _routes = new Dictionary<string, string>(StringComparer.Ordinal);

        // typeof(App) ancora l'assembly dei componenti dell'app.
        foreach (var type in typeof(App).Assembly.GetTypes())
        {
            if (!typeof(IComponent).IsAssignableFrom(type))
            {
                continue;
            }
            // Prima rotta dichiarata (le pagine qui hanno una sola @page).
            var route = type.GetCustomAttributes<RouteAttribute>().FirstOrDefault();
            if (route is not null && !_routes.ContainsKey(type.Name))
            {
                _routes[type.Name] = route.Template;
            }
        }
    }

    public string? GetRoute(string pageClassName)
        => _routes.TryGetValue(pageClassName, out var route) ? route : null;
}
