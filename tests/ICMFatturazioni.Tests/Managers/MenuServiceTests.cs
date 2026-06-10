using System.Security.Claims;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;
using Microsoft.AspNetCore.Components.Authorization;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test del <c>MenuService</c> (calcolo del menu visibile e dei diritti di
/// accesso per l'utente corrente). Verifica le regole fisse:
///   - Superadmin → tutto, comprese le pagine Superadmin-only (log errori);
///   - Admin → tutto tranne le pagine Superadmin-only;
///   - ruolo custom → solo le pagine mappate, MAI i gruppi SoloAdmin;
///   - override per-utente che SOSTITUISCE il ruolo;
///   - pagine di sistema sempre accessibili.
/// L'identità è simulata con <see cref="TestAuthStateProvider"/>.
/// </summary>
public class MenuServiceTests
{
    private static readonly Guid GruppoTabelle = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid GruppoAdmin = Guid.Parse("a0000000-0000-0000-0000-000000000002");
    private static readonly Guid SubAnagrafiche = Guid.Parse("b0000000-0000-0000-0000-000000000001");
    private static readonly Guid SubBanche = Guid.Parse("b0000000-0000-0000-0000-000000000002");
    private static readonly Guid SubGestioneUtenti = Guid.Parse("b0000000-0000-0000-0000-000000000003");

    private static readonly Guid IdRuoloCustom = Guid.Parse("c0000000-0000-0000-0000-000000000001");
    private static readonly Guid IdUtente = Guid.Parse("d0000000-0000-0000-0000-000000000001");

    /// <summary>
    /// Compone un repo con: gruppo "Tabelle" (Anagrafiche, Banche) e gruppo
    /// "Amministrazione" SoloAdmin (Gestione utenti).
    /// </summary>
    private static FakeMenuRepository NewRepo()
    {
        var repo = new FakeMenuRepository();
        repo.Menus.Add(new Menu { IdMenu = GruppoTabelle, DescrizioneMenu = "Tabelle", Ordine = 10 });
        repo.Menus.Add(new Menu { IdMenu = GruppoAdmin, DescrizioneMenu = "Amministrazione", Ordine = 90, SoloAdmin = true });
        repo.SottoMenus.Add(new SottoMenu { IdSottoMenu = SubAnagrafiche, IdMenu = GruppoTabelle, Descrizione = "Anagrafiche", PaginaRazor = "Anagrafiche", Ordine = 10 });
        repo.SottoMenus.Add(new SottoMenu { IdSottoMenu = SubBanche, IdMenu = GruppoTabelle, Descrizione = "Banche", PaginaRazor = "Banche", Ordine = 20 });
        repo.SottoMenus.Add(new SottoMenu { IdSottoMenu = SubGestioneUtenti, IdMenu = GruppoAdmin, Descrizione = "Gestione utenti", PaginaRazor = "GestioneUtenti", Ordine = 10 });
        return repo;
    }

    private static MenuService NewSut(FakeMenuRepository repo, TestAuthStateProvider auth)
        => new(repo, auth);

    // =================================================================
    // Pagine di sistema: sempre lecite (anche senza claim/identità)
    // =================================================================

    [Theory]
    [InlineData("Home")]
    [InlineData("Dashboard")]
    [InlineData("Login")]
    [InlineData("AccessDenied")]
    [InlineData("NotFound")]
    [InlineData("Attiva")]          // magic-link attivazione (anonimo)
    [InlineData("ResetPassword")]   // magic-link reset (anonimo)
    [InlineData("ForgotPassword")]  // password dimenticata (anonimo)
    [InlineData("")]
    public async Task PuoAccedereAsync_PagineDiSistema_SempreVere(string pagina)
    {
        var sut = NewSut(NewRepo(), TestAuthStateProvider.Anonimo());
        Assert.True(await sut.PuoAccedereAsync(pagina));
    }

    // =================================================================
    // Superadmin
    // =================================================================

    [Fact]
    public async Task Superadmin_PuoAccedereATutto_IncluseLePagineSoloSuperadmin()
    {
        var sut = NewSut(NewRepo(), TestAuthStateProvider.Superadmin());

        Assert.True(await sut.PuoAccedereAsync("Anagrafiche"));
        Assert.True(await sut.PuoAccedereAsync("GestioneUtenti"));
        Assert.True(await sut.PuoAccedereAsync("LogErrors"));   // solo-superadmin
    }

    [Fact]
    public async Task Superadmin_VedeTuttiIGruppiCompresoSoloAdmin()
    {
        var sut = NewSut(NewRepo(), TestAuthStateProvider.Superadmin());

        var albero = await sut.GetMenuVisibileAsync();

        Assert.Contains(albero, n => n.Descrizione == "Tabelle");
        Assert.Contains(albero, n => n.Descrizione == "Amministrazione");
    }

    // =================================================================
    // Admin
    // =================================================================

    [Fact]
    public async Task Admin_AccedeATuttoTranneLePagineSoloSuperadmin()
    {
        var sut = NewSut(NewRepo(), TestAuthStateProvider.Admin());

        Assert.True(await sut.PuoAccedereAsync("Anagrafiche"));
        Assert.True(await sut.PuoAccedereAsync("GestioneUtenti"));
        Assert.False(await sut.PuoAccedereAsync("LogErrors"));   // negato all'Admin
    }

    [Fact]
    public async Task Admin_VedeAncheIlGruppoSoloAdmin()
    {
        var sut = NewSut(NewRepo(), TestAuthStateProvider.Admin());

        var albero = await sut.GetMenuVisibileAsync();

        Assert.Contains(albero, n => n.Descrizione == "Amministrazione");
    }

    // =================================================================
    // Ruolo custom: solo le pagine mappate
    // =================================================================

    [Fact]
    public async Task RuoloCustom_AccedeSoloAllePagineMappateAlRuolo()
    {
        var repo = NewRepo();
        repo.MenuRuolo[IdRuoloCustom] = new HashSet<Guid> { GruppoTabelle };
        repo.SottoMenuRuolo[IdRuoloCustom] = new HashSet<Guid> { SubAnagrafiche };   // solo Anagrafiche
        var sut = NewSut(repo, TestAuthStateProvider.Custom(IdRuoloCustom, IdUtente));

        Assert.True(await sut.PuoAccedereAsync("Anagrafiche"));
        Assert.False(await sut.PuoAccedereAsync("Banche"));        // non mappata
        Assert.False(await sut.PuoAccedereAsync("GestioneUtenti")); // gruppo admin-only
    }

    [Fact]
    public async Task RuoloCustom_SenzaMapping_NonAccedeANulla()
    {
        var sut = NewSut(NewRepo(), TestAuthStateProvider.Custom(IdRuoloCustom, IdUtente));

        Assert.False(await sut.PuoAccedereAsync("Anagrafiche"));
    }

    [Fact]
    public async Task RuoloCustom_NonVedeMaiIlGruppoSoloAdmin_AncheSeMappatoPerErrore()
    {
        var repo = NewRepo();
        // Mapping "sbagliato" che include il gruppo admin-only: il servizio deve
        // comunque nasconderlo (difesa indipendente dai mapping).
        repo.MenuRuolo[IdRuoloCustom] = new HashSet<Guid> { GruppoTabelle, GruppoAdmin };
        repo.SottoMenuRuolo[IdRuoloCustom] = new HashSet<Guid> { SubAnagrafiche, SubGestioneUtenti };
        var sut = NewSut(repo, TestAuthStateProvider.Custom(IdRuoloCustom, IdUtente));

        var albero = await sut.GetMenuVisibileAsync();

        Assert.Contains(albero, n => n.Descrizione == "Tabelle");
        Assert.DoesNotContain(albero, n => n.Descrizione == "Amministrazione");
        Assert.False(await sut.PuoAccedereAsync("GestioneUtenti"));
    }

    // =================================================================
    // Override per-utente: SOSTITUISCE il ruolo
    // =================================================================

    [Fact]
    public async Task OverrideUtente_SostituisceIlRuolo()
    {
        var repo = NewRepo();
        // Il ruolo darebbe accesso a Anagrafiche...
        repo.MenuRuolo[IdRuoloCustom] = new HashSet<Guid> { GruppoTabelle };
        repo.SottoMenuRuolo[IdRuoloCustom] = new HashSet<Guid> { SubAnagrafiche };
        // ...ma l'override dell'utente concede SOLO Banche (e non Anagrafiche).
        repo.MenuUtente[IdUtente] = new HashSet<Guid> { GruppoTabelle };
        repo.SottoMenuUtente[IdUtente] = new HashSet<Guid> { SubBanche };
        var sut = NewSut(repo, TestAuthStateProvider.Custom(IdRuoloCustom, IdUtente));

        Assert.True(await sut.PuoAccedereAsync("Banche"));       // dall'override
        Assert.False(await sut.PuoAccedereAsync("Anagrafiche")); // il ruolo è ignorato
    }

    // =================================================================
    // Provider di identità per i test
    // =================================================================

    private sealed class TestAuthStateProvider : AuthenticationStateProvider
    {
        private readonly ClaimsPrincipal _user;
        private TestAuthStateProvider(ClaimsPrincipal user) => _user = user;

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
            => Task.FromResult(new AuthenticationState(_user));

        public static TestAuthStateProvider Anonimo()
            => new(new ClaimsPrincipal(new ClaimsIdentity()));   // non autenticato

        public static TestAuthStateProvider Superadmin()
            => Con(new Claim("ruolo_codice", RuoliSistema.Superadmin));

        public static TestAuthStateProvider Admin()
            => Con(new Claim("ruolo_codice", RuoliSistema.Admin));

        public static TestAuthStateProvider Custom(Guid idRuolo, Guid idUtente)
            => Con(
                new Claim("id_ruolo", idRuolo.ToString()),
                new Claim(ClaimTypes.NameIdentifier, idUtente.ToString()));

        private static TestAuthStateProvider Con(params Claim[] claims)
        {
            var identity = new ClaimsIdentity(claims, authenticationType: "Test");
            return new TestAuthStateProvider(new ClaimsPrincipal(identity));
        }
    }
}
