using ICMFatturazioni.Web.Navigation;
using MudBlazor;

namespace ICMFatturazioni.Tests.Navigation;

/// <summary>
/// Test del resolver delle icone Material. Verifica il mapping nome → SVG, il
/// fallback su "Circle" per nome assente/sconosciuto e la coerenza della cache.
/// </summary>
public class IconaResolverTests
{
    [Fact]
    public void Risolvi_NomeValido_RestituisceLaStringaSvgDiMudBlazor()
    {
        var atteso = Icons.Material.Filled.PeopleAlt;
        Assert.Equal(atteso, IconaResolver.Risolvi("PeopleAlt"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Risolvi_NomeNulloOVuoto_RestituisceIlDefaultCircle(string? nome)
    {
        Assert.Equal(Icons.Material.Filled.Circle, IconaResolver.Risolvi(nome));
    }

    [Fact]
    public void Risolvi_NomeSconosciuto_RestituisceIlDefaultCircle()
    {
        Assert.Equal(Icons.Material.Filled.Circle, IconaResolver.Risolvi("IconaCheNonEsisteDavvero"));
    }

    [Fact]
    public void Risolvi_StessoNome_RestituisceSempreLoStessoValore()
    {
        // Copre il ramo "cache hit": la seconda risoluzione deve coincidere.
        var primo = IconaResolver.Risolvi("Save");
        var secondo = IconaResolver.Risolvi("Save");
        Assert.Equal(primo, secondo);
        Assert.Equal(Icons.Material.Filled.Save, secondo);
    }
}
