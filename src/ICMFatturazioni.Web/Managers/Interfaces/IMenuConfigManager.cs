using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Managers.Interfaces;

/// <summary>Mapping di visibilità di un ruolo: id dei menu e sottomenu assegnati.</summary>
public sealed record MappingRuolo(IReadOnlySet<Guid> MenuIds, IReadOnlySet<Guid> SottoMenuIds);

/// <summary>
/// Logica di CONFIGURAZIONE del menu dinamico per ruolo (matrice ruolo×menu,
/// T3c). Distinto da <see cref="IMenuService"/>, che invece calcola il menu
/// dell'utente corrente a runtime.
/// </summary>
public interface IMenuConfigManager
{
    Task<IReadOnlyList<Menu>> GetMenusAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SottoMenu>> GetSottoMenusAsync(CancellationToken cancellationToken = default);

    /// <summary>Mapping attuale del ruolo (menu e sottomenu spuntati).</summary>
    Task<MappingRuolo> GetMappingRuoloAsync(Guid idRuolo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Salva il mapping del ruolo. I menu-gruppo dei sottomenu selezionati
    /// vengono inclusi automaticamente, così il gruppo compare nel NavMenu.
    /// </summary>
    Task SalvaMappingRuoloAsync(Guid idRuolo, IReadOnlyCollection<Guid> menuIds, IReadOnlyCollection<Guid> sottoMenuIds, CancellationToken cancellationToken = default);
}
