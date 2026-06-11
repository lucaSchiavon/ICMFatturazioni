namespace ICMFatturazioni.Web.Repositories;

/// <summary>
/// Helper condivisi dalle ricerche paginate dei repository (Log, Audit): difesa
/// sulla paginazione e neutralizzazione dei metacaratteri LIKE.
/// </summary>
internal static class SqlRicerca
{
    /// <summary>Pagina >= 1; dimensione fra 1 e 200 (default 25 fuori range).</summary>
    public static (int Pagina, int Dimensione) NormalizzaPaginazione(int pagina, int dimensione)
        => (pagina < 1 ? 1 : pagina, dimensione is < 1 or > 200 ? 25 : dimensione);

    /// <summary>Neutralizza i metacaratteri LIKE (<c>[ % _</c>) in un input di ricerca.</summary>
    public static string EscapeLike(string value)
        => value.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
}
