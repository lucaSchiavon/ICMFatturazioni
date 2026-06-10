using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Doppio in-memory di <see cref="IMenuRepository"/> per i test di
/// <c>MenuService</c> e <c>MenuConfigManager</c>. Espone gli store di menu,
/// sottomenu e dei mapping (ruolo/utente) così i test possono comporre lo
/// scenario e verificare l'esito del salvataggio.
/// </summary>
internal sealed class FakeMenuRepository : IMenuRepository
{
    public List<Menu> Menus { get; } = new();
    public List<SottoMenu> SottoMenus { get; } = new();

    public Dictionary<Guid, HashSet<Guid>> MenuRuolo { get; } = new();
    public Dictionary<Guid, HashSet<Guid>> SottoMenuRuolo { get; } = new();
    public Dictionary<Guid, HashSet<Guid>> MenuUtente { get; } = new();
    public Dictionary<Guid, HashSet<Guid>> SottoMenuUtente { get; } = new();

    public Task<IReadOnlyList<Menu>> GetMenusAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Menu>>(Menus.OrderBy(m => m.Ordine).ToList());

    public Task<IReadOnlyList<SottoMenu>> GetSottoMenusAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SottoMenu>>(SottoMenus.OrderBy(s => s.Ordine).ToList());

    public Task<IReadOnlySet<Guid>> GetMenuRuoloIdsAsync(Guid idRuolo, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlySet<Guid>>(MenuRuolo.TryGetValue(idRuolo, out var s) ? s : new HashSet<Guid>());

    public Task<IReadOnlySet<Guid>> GetSottoMenuRuoloIdsAsync(Guid idRuolo, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlySet<Guid>>(SottoMenuRuolo.TryGetValue(idRuolo, out var s) ? s : new HashSet<Guid>());

    public Task SetMappingRuoloAsync(Guid idRuolo, IReadOnlyCollection<Guid> menuIds, IReadOnlyCollection<Guid> sottoMenuIds, CancellationToken cancellationToken = default)
    {
        MenuRuolo[idRuolo] = menuIds.ToHashSet();
        SottoMenuRuolo[idRuolo] = sottoMenuIds.ToHashSet();
        return Task.CompletedTask;
    }

    public Task<IReadOnlySet<Guid>> GetMenuUtenteIdsAsync(Guid idUtente, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlySet<Guid>>(MenuUtente.TryGetValue(idUtente, out var s) ? s : new HashSet<Guid>());

    public Task<IReadOnlySet<Guid>> GetSottoMenuUtenteIdsAsync(Guid idUtente, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlySet<Guid>>(SottoMenuUtente.TryGetValue(idUtente, out var s) ? s : new HashSet<Guid>());

    public Task SetMappingUtenteAsync(Guid idUtente, IReadOnlyCollection<Guid> menuIds, IReadOnlyCollection<Guid> sottoMenuIds, CancellationToken cancellationToken = default)
    {
        MenuUtente[idUtente] = menuIds.ToHashSet();
        SottoMenuUtente[idUtente] = sottoMenuIds.ToHashSet();
        return Task.CompletedTask;
    }
}
