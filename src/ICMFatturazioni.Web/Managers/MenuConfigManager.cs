using ICMFatturazioni.Web.Auditing;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Managers;

/// <summary>Implementazione di <see cref="IMenuConfigManager"/>.</summary>
internal sealed class MenuConfigManager : IMenuConfigManager
{
    private readonly IMenuRepository _menuRepository;
    private readonly IAuditManager _audit;

    public MenuConfigManager(IMenuRepository menuRepository, IAuditManager audit)
    {
        _menuRepository = menuRepository;
        _audit = audit;
    }

    public Task<IReadOnlyList<Menu>> GetMenusAsync(CancellationToken cancellationToken = default)
        => _menuRepository.GetMenusAsync(cancellationToken);

    public Task<IReadOnlyList<SottoMenu>> GetSottoMenusAsync(CancellationToken cancellationToken = default)
        => _menuRepository.GetSottoMenusAsync(cancellationToken);

    public async Task<MappingMenu> GetMappingRuoloAsync(Guid idRuolo, CancellationToken cancellationToken = default)
    {
        var menu = await _menuRepository.GetMenuRuoloIdsAsync(idRuolo, cancellationToken);
        var sotto = await _menuRepository.GetSottoMenuRuoloIdsAsync(idRuolo, cancellationToken);
        return new MappingMenu(menu, sotto);
    }

    public async Task SalvaMappingRuoloAsync(Guid idRuolo, IReadOnlyCollection<Guid> menuIds, IReadOnlyCollection<Guid> sottoMenuIds, CancellationToken cancellationToken = default)
    {
        var menuFinali = await IncludiGruppiAsync(menuIds, sottoMenuIds, cancellationToken);
        await _menuRepository.SetMappingRuoloAsync(idRuolo, menuFinali, sottoMenuIds, cancellationToken);
        await _audit.RegistraModificaAsync("PermessiRuolo", idRuolo,
            $"Permessi ruolo aggiornati: {menuFinali.Count} menu, {sottoMenuIds.Count} sottovoci.",
            AuditDettaglio.Snapshot(new { Menu = menuFinali, SottoMenu = sottoMenuIds }), cancellationToken);
    }

    // --- Per utente / override (T3d) ---

    public async Task<bool> HasPersonalizzazioneUtenteAsync(Guid idUtente, CancellationToken cancellationToken = default)
    {
        var menu = await _menuRepository.GetMenuUtenteIdsAsync(idUtente, cancellationToken);
        if (menu.Count > 0) return true;
        var sotto = await _menuRepository.GetSottoMenuUtenteIdsAsync(idUtente, cancellationToken);
        return sotto.Count > 0;
    }

    public async Task<MappingMenu> GetMappingUtenteAsync(Guid idUtente, CancellationToken cancellationToken = default)
    {
        var menu = await _menuRepository.GetMenuUtenteIdsAsync(idUtente, cancellationToken);
        var sotto = await _menuRepository.GetSottoMenuUtenteIdsAsync(idUtente, cancellationToken);
        return new MappingMenu(menu, sotto);
    }

    public async Task SalvaMappingUtenteAsync(Guid idUtente, IReadOnlyCollection<Guid> menuIds, IReadOnlyCollection<Guid> sottoMenuIds, CancellationToken cancellationToken = default)
    {
        var menuFinali = await IncludiGruppiAsync(menuIds, sottoMenuIds, cancellationToken);
        await _menuRepository.SetMappingUtenteAsync(idUtente, menuFinali, sottoMenuIds, cancellationToken);
        await _audit.RegistraModificaAsync("PermessiUtente", idUtente,
            $"Override permessi utente impostato: {menuFinali.Count} menu, {sottoMenuIds.Count} sottovoci.",
            AuditDettaglio.Snapshot(new { Menu = menuFinali, SottoMenu = sottoMenuIds }), cancellationToken);
    }

    public async Task RimuoviPersonalizzazioneUtenteAsync(Guid idUtente, CancellationToken cancellationToken = default)
    {
        await _menuRepository.SetMappingUtenteAsync(idUtente, Array.Empty<Guid>(), Array.Empty<Guid>(), cancellationToken);
        await _audit.RegistraEliminazioneAsync("PermessiUtente", idUtente,
            "Override permessi utente rimosso (ritorno al ruolo).", cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Aggiunge ai menu selezionati i gruppi-padre dei sottomenu spuntati: senza
    /// il gruppo, il NavMenu non mostrerebbe la sottovoce.
    /// </summary>
    private async Task<HashSet<Guid>> IncludiGruppiAsync(IReadOnlyCollection<Guid> menuIds, IReadOnlyCollection<Guid> sottoMenuIds, CancellationToken cancellationToken)
    {
        var sottoMenus = await _menuRepository.GetSottoMenusAsync(cancellationToken);
        var menuFinali = new HashSet<Guid>(menuIds);
        var sottoSelezionati = sottoMenuIds.ToHashSet();
        foreach (var s in sottoMenus)
        {
            if (sottoSelezionati.Contains(s.IdSottoMenu))
            {
                menuFinali.Add(s.IdMenu);
            }
        }
        return menuFinali;
    }
}
