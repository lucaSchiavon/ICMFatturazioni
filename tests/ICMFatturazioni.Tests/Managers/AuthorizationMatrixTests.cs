using System.Reflection;
using System.Security.Claims;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Congela la MATRICE DI AUTORIZZAZIONE reale dell'applicativo. È il complemento
/// di <see cref="MenuServiceTests"/> (che verifica il MOTORE con un menu
/// sintetico): qui si congela la classificazione di accesso delle PAGINE VERE.
///
/// Analogo — nel modello DB-driven di ICMFatturazioni — dell'AuthorizationMatrixTests
/// di ICMVerbali. Poiché qui l'enforcement NON è un attributo [Authorize] sulla
/// pagina ma la guardia di rotta (MenuRouteView → IMenuService) sopra i menu per
/// ruolo, il test lavora su due livelli:
///
///   1) INVENTARIO (regressione dichiarativa): ogni pagina instradabile (@page)
///      dell'assembly Web DEVE comparire nella <see cref="Matrice"/> con un
///      livello di accesso esplicito. Aggiungere una pagina/report riservato
///      dimenticando di classificarne l'accesso rende ROSSO il test in build —
///      esattamente lo scopo del test di Verbali.
///   2) ENFORCEMENT: la classificazione è verificata contro il MenuService reale
///      per i quattro profili (Superadmin, Admin, ruolo operativo, anonimo),
///      così una modifica alle regole di accesso che diverga dalla matrice
///      congelata viene intercettata.
///
/// Evidenza per ISO 27001: A5.15 (controllo degli accessi), A5.18 (diritti di
/// accesso), A8.5 (autenticazione/accesso sicuro). La prova funzionale end-to-end
/// (navigazione via URL per ruolo) resta complementare a questo congelamento.
/// </summary>
public class AuthorizationMatrixTests
{
    /// <summary>Livello di accesso atteso di una pagina.</summary>
    private enum Accesso
    {
        /// <summary>Sempre accessibile: home, dashboard, login, errori, magic-link
        /// (coincide con l'insieme "pagine di sistema" del MenuService).</summary>
        Sistema,

        /// <summary>Riservata al solo Superadmin (diagnostica: log errori).</summary>
        SoloSuperadmin,

        /// <summary>Gruppo Amministrazione: accessibile ad Admin e Superadmin,
        /// negata ai ruoli operativi (gestione utenti/ruoli/permessi, audit,
        /// dati azienda).</summary>
        Amministrazione,

        /// <summary>Pagina di dominio, assegnabile ai ruoli operativi tramite i
        /// menu (tutte le maschere del gestionale, inclusi i consulenti).</summary>
        Operativa,
    }

    // ─────────────────────────────────────────────────────────────────────────
    // La matrice congelata: pagina Razor (nome classe = chiave della guardia di
    // rotta e del menu) → livello di accesso atteso.
    // MANUTENZIONE: se aggiungi una pagina con @page, aggiungila qui scegliendone
    // consapevolmente il livello; il test InventarioPagine_* te lo ricorderà.
    // ─────────────────────────────────────────────────────────────────────────
    private static readonly IReadOnlyDictionary<string, Accesso> Matrice =
        new Dictionary<string, Accesso>(StringComparer.Ordinal)
        {
            // — Sistema (sempre lecite) —
            ["Home"]           = Accesso.Sistema,
            ["Dashboard"]      = Accesso.Sistema,
            ["Login"]          = Accesso.Sistema,
            ["AccessDenied"]   = Accesso.Sistema,
            ["NotFound"]       = Accesso.Sistema,
            ["Attiva"]         = Accesso.Sistema,   // magic-link attivazione (anonimo)
            ["ResetPassword"]  = Accesso.Sistema,   // magic-link reset (anonimo)
            ["ForgotPassword"] = Accesso.Sistema,   // password dimenticata (anonimo)

            // — Solo Superadmin —
            ["LogPage"]        = Accesso.SoloSuperadmin,   // /admin/log

            // — Amministrazione (Admin + Superadmin) —
            ["GestioneUtenti"] = Accesso.Amministrazione,
            ["GestioneRuoli"]  = Accesso.Amministrazione,
            ["PermessiMenu"]   = Accesso.Amministrazione,
            ["PermessiUtente"] = Accesso.Amministrazione,
            ["AuditPage"]      = Accesso.Amministrazione,
            ["DatiAzienda"]    = Accesso.Amministrazione,

            // — Operative (maschere di dominio, assegnabili ai ruoli) —
            ["Anagrafiche"]              = Accesso.Operativa,
            ["DescrizioniAttivita"]      = Accesso.Operativa,
            ["BancheAppoggio"]           = Accesso.Operativa,
            ["CodiciIVA"]                = Accesso.Operativa,
            ["TipiPagamento"]            = Accesso.Operativa,
            ["CodiciPagamento"]          = Accesso.Operativa,
            ["AliquoteVigenti"]          = Accesso.Operativa,
            ["TipiAttivitaStudio"]       = Accesso.Operativa,
            ["TipiDettAttivitaStudio"]   = Accesso.Operativa,
            ["GestAttivitaStudio"]       = Accesso.Operativa,
            ["Cantieri"]                 = Accesso.Operativa,
            ["SpeseAnticipate"]          = Accesso.Operativa,
            ["AvvisiFattura"]            = Accesso.Operativa,
            ["StampeFatture"]            = Accesso.Operativa,
            ["StampaScadenze"]           = Accesso.Operativa,
            ["GestioneXml"]              = Accesso.Operativa,
            ["ConsultazioneVerbali"]     = Accesso.Operativa,
            // Modulo Attività Consulenti
            ["Consulenti"]               = Accesso.Operativa,
            ["TipiAttivitaConsulenti"]   = Accesso.Operativa,
            ["GestAttivitaConsulenti"]   = Accesso.Operativa,
            ["GestPagamentiConsulenti"]  = Accesso.Operativa,
            ["SchedeAttivitaConsulenti"] = Accesso.Operativa,
        };

    // ═════════════════════════════════════════════════════════════════════════
    // 1. INVENTARIO — ogni pagina reale deve avere una classificazione esplicita
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void InventarioPagine_OgniPaginaInstradabile_EClassificata()
    {
        var nonClassificate = PagineInstradabili()
            .Where(p => !Matrice.ContainsKey(p))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        Assert.True(nonClassificate.Count == 0,
            "Pagine instradabili (@page) senza livello di accesso nella Matrice. " +
            "Aggiungile scegliendone consapevolmente il livello: " +
            string.Join(", ", nonClassificate));
    }

    [Fact]
    public void InventarioPagine_NessunaVoceObsoleta_NellaMatrice()
    {
        var reali = PagineInstradabili();
        var obsolete = Matrice.Keys
            .Where(k => !reali.Contains(k))
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        Assert.True(obsolete.Count == 0,
            "Voci nella Matrice che non corrispondono a nessuna pagina reale " +
            "(pagina rinominata/rimossa?): " + string.Join(", ", obsolete));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 2. ENFORCEMENT — la classificazione è verificata contro il MenuService reale
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Superadmin_AccedeAOgniPagina()
    {
        var (repo, _) = NewRepoDallaMatrice();
        var sut = new MenuService(repo, Auth.Superadmin());

        foreach (var pagina in Matrice.Keys)
        {
            Assert.True(await sut.PuoAccedereAsync(pagina),
                $"Il Superadmin deve poter accedere a '{pagina}'.");
        }
    }

    [Fact]
    public async Task Admin_AccedeATutto_TranneLePagineSoloSuperadmin()
    {
        var (repo, _) = NewRepoDallaMatrice();
        var sut = new MenuService(repo, Auth.Admin());

        foreach (var (pagina, accesso) in Matrice)
        {
            var atteso = accesso != Accesso.SoloSuperadmin;   // solo le superadmin-only sono negate
            Assert.Equal(atteso, await sut.PuoAccedereAsync(pagina));
        }
    }

    [Fact]
    public async Task RuoloOperativo_AccedeSoloAllePagineOperativeEDiSistema()
    {
        var (repo, idRuoloOp) = NewRepoDallaMatrice();
        var sut = new MenuService(repo, Auth.Ruolo(idRuoloOp));

        foreach (var (pagina, accesso) in Matrice)
        {
            var atteso = accesso is Accesso.Sistema or Accesso.Operativa;   // niente admin/superadmin
            Assert.Equal(atteso, await sut.PuoAccedereAsync(pagina));
        }
    }

    [Fact]
    public async Task Anonimo_AccedeSoloAllePagineDiSistema()
    {
        var (repo, _) = NewRepoDallaMatrice();
        var sut = new MenuService(repo, Auth.Anonimo());

        foreach (var (pagina, accesso) in Matrice)
        {
            var atteso = accesso == Accesso.Sistema;
            Assert.Equal(atteso, await sut.PuoAccedereAsync(pagina));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helper
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Scopre via reflection tutte le pagine Razor instradabili (con almeno un
    /// <see cref="RouteAttribute"/>, cioè una direttiva <c>@page</c>) nell'assembly
    /// Web. La chiave è il nome della classe, la stessa usata dalla guardia di
    /// rotta (<c>routeData.PageType.Name</c>) e memorizzata nel menu.
    /// </summary>
    private static IReadOnlySet<string> PagineInstradabili()
        => typeof(Menu).Assembly.GetTypes()
            .Where(t => typeof(ComponentBase).IsAssignableFrom(t) && !t.IsAbstract)
            .Where(t => t.GetCustomAttributes<RouteAttribute>(inherit: false).Any())
            .Select(t => t.Name)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Compone un <see cref="FakeMenuRepository"/> che rispecchia la
    /// <see cref="Matrice"/>: gruppo "Amministrazione" (SoloAdmin) con le pagine
    /// admin e la pagina solo-Superadmin (flag SoloSuperadmin), gruppo "Operative"
    /// con le pagine di dominio, mappato a un ruolo operativo. Le pagine di
    /// sistema non entrano nel menu (gestite dal MenuService come sempre lecite).
    /// </summary>
    private static (FakeMenuRepository repo, Guid idRuoloOp) NewRepoDallaMatrice()
    {
        var repo = new FakeMenuRepository();
        var gruppoAmm = Guid.NewGuid();
        var gruppoOp = Guid.NewGuid();
        repo.Menus.Add(new Menu { IdMenu = gruppoAmm, DescrizioneMenu = "Amministrazione", Ordine = 90, SoloAdmin = true });
        repo.Menus.Add(new Menu { IdMenu = gruppoOp, DescrizioneMenu = "Operative", Ordine = 10 });

        var sottoOperative = new List<Guid>();
        var ordine = 0;
        foreach (var (pagina, accesso) in Matrice)
        {
            if (accesso == Accesso.Sistema) continue;   // sempre lecita, fuori dal menu

            var id = Guid.NewGuid();
            switch (accesso)
            {
                case Accesso.SoloSuperadmin:
                    repo.SottoMenus.Add(new SottoMenu { IdSottoMenu = id, IdMenu = gruppoAmm, Descrizione = pagina, PaginaRazor = pagina, Ordine = ordine++, SoloSuperadmin = true });
                    break;
                case Accesso.Amministrazione:
                    repo.SottoMenus.Add(new SottoMenu { IdSottoMenu = id, IdMenu = gruppoAmm, Descrizione = pagina, PaginaRazor = pagina, Ordine = ordine++ });
                    break;
                case Accesso.Operativa:
                    repo.SottoMenus.Add(new SottoMenu { IdSottoMenu = id, IdMenu = gruppoOp, Descrizione = pagina, PaginaRazor = pagina, Ordine = ordine++ });
                    sottoOperative.Add(id);
                    break;
            }
        }

        // Il ruolo operativo vede solo il gruppo Operative e le sue sottovoci.
        var idRuoloOp = Guid.NewGuid();
        repo.MenuRuolo[idRuoloOp] = new HashSet<Guid> { gruppoOp };
        repo.SottoMenuRuolo[idRuoloOp] = sottoOperative.ToHashSet();
        return (repo, idRuoloOp);
    }

    /// <summary>Provider d'identità per i test: costruisce i claim che il
    /// MenuService legge (<c>ruolo_codice</c>, <c>id_ruolo</c>, NameIdentifier).</summary>
    private sealed class Auth : AuthenticationStateProvider
    {
        private readonly ClaimsPrincipal _user;
        private Auth(ClaimsPrincipal user) => _user = user;

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
            => Task.FromResult(new AuthenticationState(_user));

        public static Auth Anonimo() => new(new ClaimsPrincipal(new ClaimsIdentity()));
        public static Auth Superadmin() => Con(new Claim("ruolo_codice", RuoliSistema.Superadmin));
        public static Auth Admin() => Con(new Claim("ruolo_codice", RuoliSistema.Admin));

        // Ruolo custom: id_ruolo valorizzato, utente senza override → si applicano
        // i permessi del ruolo (mapping MenuRuolo/SottoMenuRuolo).
        public static Auth Ruolo(Guid idRuolo) => Con(
            new Claim("id_ruolo", idRuolo.ToString()),
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));

        private static Auth Con(params Claim[] claims)
            => new(new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test")));
    }
}
