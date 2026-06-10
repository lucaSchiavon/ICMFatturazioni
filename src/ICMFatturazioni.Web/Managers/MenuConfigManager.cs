using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Managers;

/// <summary>Implementazione di <see cref="IMenuConfigManager"/>.</summary>
internal sealed class MenuConfigManager : IMenuConfigManager
{
    private readonly IMenuRepository _menuRepository;

    public MenuConfigManager(IMenuRepository menuRepository)
    {
        _menuRepository = menuRepository;
    }

    public Task<IReadOnlyList<Menu>> GetMenusAsync(CancellationToken cancellationToken = default)
        => _menuRepository.GetMenusAsync(cancellationToken);

    public Task<IReadOnlyList<SottoMenu>> GetSottoMenusAsync(CancellationToken cancellationToken = default)
        => _menuRepository.GetSottoMenusAsync(cancellationToken);

    public async Task<MappingRuolo> GetMappingRuoloAsync(Guid idRuolo, CancellationToken cancellationToken = default)
    {
        var menu = await _menuRepository.GetMenuRuoloIdsAsync(idRuolo, cancellationToken);
        var sotto = await _menuRepository.GetSottoMenuRuoloIdsAsync(idRuolo, cancellationToken);
        return new MappingRuolo(menu, sotto);
    }

    public async Task SalvaMappingRuoloAsync(Guid idRuolo, IReadOnlyCollection<Guid> menuIds, IReadOnlyCollection<Guid> sottoMenuIds, CancellationToken cancellationToken = default)
    {
        // Includi automaticamente i menu-gruppo dei sottomenu selezionati: senza
        // il gruppo, il NavMenu non mostrerebbe la sottovoce.
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

        await _menuRepository.SetMappingRuoloAsync(idRuolo, menuFinali, sottoMenuIds, cancellationToken);
    }
}
