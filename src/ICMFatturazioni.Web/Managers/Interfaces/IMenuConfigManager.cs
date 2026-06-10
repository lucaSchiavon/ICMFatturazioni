using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Managers.Interfaces;

/// <summary>Mapping di visibilità: id dei menu e sottomenu selezionati.</summary>
public sealed record MappingMenu(IReadOnlySet<Guid> MenuIds, IReadOnlySet<Guid> SottoMenuIds);

/// <summary>
/// Logica di CONFIGURAZIONE del menu dinamico, per RUOLO (matrice ruolo×menu,
/// T3c) e per UTENTE (override, T3d). Distinto da <see cref="IMenuService"/>,
/// che calcola il menu dell'utente corrente a runtime.
/// </summary>
public interface IMenuConfigManager
{
    Task<IReadOnlyList<Menu>> GetMenusAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SottoMenu>> GetSottoMenusAsync(CancellationToken cancellationToken = default);

    // --- Per ruolo (T3c) ---

    /// <summary>Mapping attuale del ruolo (menu e sottomenu spuntati).</summary>
    Task<MappingMenu> GetMappingRuoloAsync(Guid idRuolo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Salva il mapping del ruolo. I menu-gruppo dei sottomenu selezionati
    /// vengono inclusi automaticamente, così il gruppo compare nel NavMenu.
    /// </summary>
    Task SalvaMappingRuoloAsync(Guid idRuolo, IReadOnlyCollection<Guid> menuIds, IReadOnlyCollection<Guid> sottoMenuIds, CancellationToken cancellationToken = default);

    // --- Per utente / override (T3d) ---

    /// <summary>True se l'utente ha un override personalizzato (righe MenuUtente/SottoMenuUtente).</summary>
    Task<bool> HasPersonalizzazioneUtenteAsync(Guid idUtente, CancellationToken cancellationToken = default);

    /// <summary>Override attuale dell'utente (vuoto se segue il ruolo).</summary>
    Task<MappingMenu> GetMappingUtenteAsync(Guid idUtente, CancellationToken cancellationToken = default);

    /// <summary>
    /// Salva l'override dell'utente (SOSTITUISCE il ruolo). Auto-include i
    /// menu-gruppo dei sottomenu selezionati.
    /// </summary>
    Task SalvaMappingUtenteAsync(Guid idUtente, IReadOnlyCollection<Guid> menuIds, IReadOnlyCollection<Guid> sottoMenuIds, CancellationToken cancellationToken = default);

    /// <summary>Rimuove l'override: l'utente torna a seguire il ruolo.</summary>
    Task RimuoviPersonalizzazioneUtenteAsync(Guid idUtente, CancellationToken cancellationToken = default);
}
