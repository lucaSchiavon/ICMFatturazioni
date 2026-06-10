namespace ICMFatturazioni.Web.Models;

/// <summary>
/// Nodo dell'albero di menu già FILTRATO per l'utente corrente, pronto per il
/// rendering del <c>NavMenu</c>. Prodotto da <c>IMenuService</c> a partire da
/// <c>fatt.Menu</c>/<c>fatt.SottoMenu</c> + mapping di visibilità.
/// </summary>
public sealed class MenuNodo
{
    /// <summary>Etichetta mostrata in UI.</summary>
    public required string Descrizione { get; init; }

    /// <summary>
    /// Nome pagina Razor (href = "/" + …) a cui il nodo punta; <c>null</c> per
    /// i gruppi contenitori.
    /// </summary>
    public string? PaginaRazor { get; init; }

    /// <summary>Nome icona Material (es. "PeopleAlt").</summary>
    public string? Icona { get; init; }

    /// <summary>Funzionalità implementata/cliccabile (false = voce in grigio).</summary>
    public bool Attivo { get; init; }

    /// <summary>Sottovoci visibili (vuoto per le foglie).</summary>
    public IReadOnlyList<MenuNodo> Figli { get; init; } = Array.Empty<MenuNodo>();

    /// <summary>True se è un gruppo espandibile (ha figli o non punta a una pagina).</summary>
    public bool EGruppo => Figli.Count > 0;
}
