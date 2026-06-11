using System.Security.Claims;
using ICMFatturazioni.Web.Authentication;
using Microsoft.AspNetCore.Components.Authorization;

namespace ICMFatturazioni.Tests.Authentication;

/// <summary>
/// Test del <c>CurrentUserAccessor</c>: estrae id (NameIdentifier) e nome (Name)
/// dai claim del principal autenticato; restituisce (null, null) per anonimo o
/// contesto non disponibile. L'identità è simulata con un
/// <see cref="AuthenticationStateProvider"/> di test.
/// </summary>
public class CurrentUserAccessorTests
{
    private sealed class TestAuthStateProvider : AuthenticationStateProvider
    {
        private readonly ClaimsPrincipal _user;
        private readonly bool _lancia;
        private TestAuthStateProvider(ClaimsPrincipal user, bool lancia = false)
        {
            _user = user;
            _lancia = lancia;
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
            => _lancia
                ? throw new InvalidOperationException("Nessun circuito autenticato.")
                : Task.FromResult(new AuthenticationState(_user));

        public static TestAuthStateProvider Anonimo()
            => new(new ClaimsPrincipal(new ClaimsIdentity()));

        public static TestAuthStateProvider Lancia()
            => new(new ClaimsPrincipal(new ClaimsIdentity()), lancia: true);

        public static TestAuthStateProvider Con(params Claim[] claims)
            => new(new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test")));
    }

    [Fact]
    public async Task GetAsync_Autenticato_RestituisceIdENome()
    {
        var id = Guid.NewGuid();
        var sut = new CurrentUserAccessor(TestAuthStateProvider.Con(
            new Claim(ClaimTypes.NameIdentifier, id.ToString()),
            new Claim(ClaimTypes.Name, "carla.verdi")));

        var (gotId, gotNome) = await sut.GetAsync();

        Assert.Equal(id, gotId);
        Assert.Equal("carla.verdi", gotNome);
    }

    [Fact]
    public async Task GetAsync_Anonimo_RestituisceNullNull()
    {
        var sut = new CurrentUserAccessor(TestAuthStateProvider.Anonimo());

        var (id, nome) = await sut.GetAsync();

        Assert.Null(id);
        Assert.Null(nome);
    }

    [Fact]
    public async Task GetAsync_ContestoNonDisponibile_NonPropaga()
    {
        // Fuori da un circuito autenticato il provider può lanciare: l'accessor
        // lo assorbe e restituisce nessun utente (niente eccezione).
        var sut = new CurrentUserAccessor(TestAuthStateProvider.Lancia());

        var (id, nome) = await sut.GetAsync();

        Assert.Null(id);
        Assert.Null(nome);
    }

    [Fact]
    public async Task GetAsync_IdNonGuid_RestituisceNomeMaIdNull()
    {
        var sut = new CurrentUserAccessor(TestAuthStateProvider.Con(
            new Claim(ClaimTypes.NameIdentifier, "non-un-guid"),
            new Claim(ClaimTypes.Name, "tizio")));

        var (id, nome) = await sut.GetAsync();

        Assert.Null(id);
        Assert.Equal("tizio", nome);
    }
}
