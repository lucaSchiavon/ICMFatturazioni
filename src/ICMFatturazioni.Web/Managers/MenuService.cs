using System.Security.Claims;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Models;
using ICMFatturazioni.Web.Repositories.Interfaces;
using Microsoft.AspNetCore.Components.Authorization;

namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Implementazione di <see cref="IMenuService"/>. Servizio SCOPED: vive per la
/// durata del circuit Blazor, quindi calcola una volta sola (lazy) l'albero
/// visibile e l'insieme delle pagine consentite per l'utente corrente, poi li
/// riusa per tutte le navigazioni di quella sessione.
/// </summary>
/// <remarks>
/// Regola di accesso (fissa, sui ruoli di sistema):
///   - Superadmin → tutto, incluse le pagine Superadmin-only (log errori);
///   - Admin → tutto tranne le pagine Superadmin-only;
///   - altri ruoli → solo le pagine dei menu/sottomenu loro assegnati
///     (MenuRuolo/SottoMenuRuolo) o concessi per utente (MenuUtente/...).
/// Le pagine "di sistema" (Home, Dashboard, Login, errori) sono sempre lecite.
/// </remarks>
internal sealed class MenuService : IMenuService
{
    // Pagine sempre accessibili (non gestite a menu). Includono le pagine
    // anonime del flusso account (T4): magic-link di attivazione/reset e
    // "password dimenticata", raggiungibili senza essere autenticati.
    private static readonly HashSet<string> PagineSistema = new(StringComparer.Ordinal)
    {
        "Home", "Dashboard", "Login", "AccessDenied", "NotFound",
        "Attiva", "ResetPassword", "ForgotPassword",
    };

    // Pagine riservate al solo Superadmin (es. log errori). Negate ad Admin e
    // a tutti gli altri. Il nome è predisposto per quando la pagina esisterà.
    private static readonly HashSet<string> PagineSoloSuperadmin = new(StringComparer.Ordinal)
    {
        "LogErrors",
    };

    private readonly IMenuRepository _menuRepository;
    private readonly AuthenticationStateProvider _authStateProvider;

    // Cache per-circuit (servizio scoped): calcolo una volta sola.
    private IReadOnlyList<MenuNodo>? _albero;
    private HashSet<string>? _pagineConsentite;
    private bool _isSuperadmin;
    private bool _isAdmin;
    private bool _caricato;

    public MenuService(IMenuRepository menuRepository, AuthenticationStateProvider authStateProvider)
    {
        _menuRepository = menuRepository;
        _authStateProvider = authStateProvider;
    }

    public async Task<IReadOnlyList<MenuNodo>> GetMenuVisibileAsync(CancellationToken cancellationToken = default)
    {
        await EnsureCaricatoAsync(cancellationToken);
        return _albero!;
    }

    public async Task<bool> PuoAccedereAsync(string pageName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(pageName) || PagineSistema.Contains(pageName))
        {
            return true;
        }

        await EnsureCaricatoAsync(cancellationToken);

        if (_isSuperadmin)
        {
            return true;
        }
        if (PagineSoloSuperadmin.Contains(pageName))
        {
            return false;   // Admin e altri: negato
        }
        if (_isAdmin)
        {
            return true;
        }
        return _pagineConsentite!.Contains(pageName);
    }

    /// <summary>
    /// Calcola albero + pagine consentite una sola volta per circuit, leggendo
    /// i claim dell'utente corrente e i mapping di visibilità dal DB.
    /// </summary>
    private async Task EnsureCaricatoAsync(CancellationToken cancellationToken)
    {
        if (_caricato)
        {
            return;
        }

        var state = await _authStateProvider.GetAuthenticationStateAsync();
        var user = state.User;

        var ruoloCodice = user.FindFirst("ruolo_codice")?.Value;
        _isSuperadmin = string.Equals(ruoloCodice, RuoliSistema.Superadmin, StringComparison.Ordinal);
        _isAdmin = string.Equals(ruoloCodice, RuoliSistema.Admin, StringComparison.Ordinal);

        Guid.TryParse(user.FindFirst("id_ruolo")?.Value, out var idRuolo);
        Guid.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var idUtente);

        var menus = await _menuRepository.GetMenusAsync(cancellationToken);
        var sottoMenus = await _menuRepository.GetSottoMenusAsync(cancellationToken);

        // Insiemi di id visibili.
        //  - Superadmin/Admin: tutto (bypass, niente query sui mapping).
        //  - Altri ruoli: se l'utente ha un OVERRIDE personalizzato (righe
        //    MenuUtente/SottoMenuUtente) quello SOSTITUISCE il ruolo; altrimenti
        //    si applicano i permessi del ruolo.
        IReadOnlySet<Guid> menuVisibili;
        IReadOnlySet<Guid> sottoVisibili;
        if (_isSuperadmin || _isAdmin)
        {
            menuVisibili = menus.Select(m => m.IdMenu).ToHashSet();
            sottoVisibili = sottoMenus.Select(s => s.IdSottoMenu).ToHashSet();
        }
        else
        {
            var userMenu = idUtente != Guid.Empty
                ? await _menuRepository.GetMenuUtenteIdsAsync(idUtente, cancellationToken)
                : (IReadOnlySet<Guid>)new HashSet<Guid>();
            var userSotto = idUtente != Guid.Empty
                ? await _menuRepository.GetSottoMenuUtenteIdsAsync(idUtente, cancellationToken)
                : (IReadOnlySet<Guid>)new HashSet<Guid>();

            if (userMenu.Count > 0 || userSotto.Count > 0)
            {
                // Override per-utente attivo.
                menuVisibili = userMenu;
                sottoVisibili = userSotto;
            }
            else if (idRuolo != Guid.Empty)
            {
                menuVisibili = await _menuRepository.GetMenuRuoloIdsAsync(idRuolo, cancellationToken);
                sottoVisibili = await _menuRepository.GetSottoMenuRuoloIdsAsync(idRuolo, cancellationToken);
            }
            else
            {
                menuVisibili = new HashSet<Guid>();
                sottoVisibili = new HashSet<Guid>();
            }
        }

        // Costruzione albero: per ogni menu, le sue sottovoci visibili.
        var nodi = new List<MenuNodo>();
        var pagine = new HashSet<string>(StringComparer.Ordinal);

        foreach (var m in menus)
        {
            // Difesa: i gruppi admin-only non compaiono mai per i non-admin,
            // a prescindere da eventuali mapping.
            if (m.SoloAdmin && !_isSuperadmin && !_isAdmin)
            {
                continue;
            }

            var figli = sottoMenus
                .Where(s => s.IdMenu == m.IdMenu && sottoVisibili.Contains(s.IdSottoMenu))
                .Select(s => new MenuNodo
                {
                    Descrizione = s.Descrizione,
                    PaginaRazor = s.PaginaRazor,
                    Icona = s.Icona,
                    Attivo = s.Attivo,
                })
                .ToList();

            foreach (var f in figli)
            {
                if (!string.IsNullOrEmpty(f.PaginaRazor))
                {
                    pagine.Add(f.PaginaRazor);
                }
            }

            // Un menu compare se: è esso stesso visibile ED è un link diretto
            // (ha una pagina), oppure ha almeno una sottovoce visibile.
            var menuVisibile = menuVisibili.Contains(m.IdMenu);
            var eLinkDiretto = !string.IsNullOrEmpty(m.PaginaRazor);

            if (figli.Count == 0 && !(menuVisibile && eLinkDiretto))
            {
                continue;
            }

            if (menuVisibile && eLinkDiretto)
            {
                pagine.Add(m.PaginaRazor!);
            }

            nodi.Add(new MenuNodo
            {
                Descrizione = m.DescrizioneMenu,
                PaginaRazor = m.PaginaRazor,
                Icona = m.Icona,
                Attivo = m.Attivo,
                Figli = figli,
            });
        }

        _albero = nodi;
        _pagineConsentite = pagine;
        _caricato = true;
    }
}
