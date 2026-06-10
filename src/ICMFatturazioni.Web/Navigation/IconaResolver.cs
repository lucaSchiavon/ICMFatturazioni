using System.Collections.Concurrent;
using System.Reflection;
using MudBlazor;

namespace ICMFatturazioni.Web.Navigation;

/// <summary>
/// Risolve il NOME di un'icona Material (es. "PeopleAlt") nella stringa SVG
/// corrispondente di MudBlazor (<c>Icons.Material.Filled.PeopleAlt</c>), via
/// riflessione con cache. Le tabelle menu memorizzano solo il nome dell'icona.
/// </summary>
public static class IconaResolver
{
    private static readonly ConcurrentDictionary<string, string> Cache = new(StringComparer.Ordinal);

    // Fallback per nome assente o non riconosciuto.
    private static readonly string Default = Icons.Material.Filled.Circle;

    public static string Risolvi(string? nome)
    {
        if (string.IsNullOrEmpty(nome))
        {
            return Default;
        }
        return Cache.GetOrAdd(nome, n =>
        {
            var field = typeof(Icons.Material.Filled).GetField(n, BindingFlags.Public | BindingFlags.Static);
            return field?.GetValue(null) as string ?? Default;
        });
    }
}
