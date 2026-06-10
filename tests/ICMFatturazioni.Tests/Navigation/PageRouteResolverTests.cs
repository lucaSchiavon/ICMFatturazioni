using ICMFatturazioni.Web.Navigation;

namespace ICMFatturazioni.Tests.Navigation;

/// <summary>
/// Test del <c>PageRouteResolver</c>, che mappa via riflessione il nome della
/// classe-pagina alla sua rotta <c>@page</c>. Verifica il caso "trovato" su
/// pagine reali e il fallback <c>null</c> per un nome inesistente.
/// </summary>
public class PageRouteResolverTests
{
    private static readonly PageRouteResolver Sut = new();

    [Theory]
    [InlineData("Login", "/login")]
    [InlineData("Dashboard", "/dashboard")]
    [InlineData("Anagrafiche", "/anagrafiche")]
    [InlineData("GestioneUtenti", "/admin/utenti")]
    public void GetRoute_PaginaEsistente_RestituisceLaRotta(string pagina, string rottaAttesa)
    {
        Assert.Equal(rottaAttesa, Sut.GetRoute(pagina));
    }

    [Fact]
    public void GetRoute_PaginaInesistente_RestituisceNull()
    {
        Assert.Null(Sut.GetRoute("PaginaCheNonEsisteDavvero"));
    }
}
