using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Repositories.Interfaces;

/// <summary>
/// Accesso dati alle tabelle del menu dinamico (<c>fatt.Menu</c>,
/// <c>fatt.SottoMenu</c> e mapping di visibilità per ruolo/utente).
/// </summary>
public interface IMenuRepository
{
    /// <summary>Tutte le voci di primo livello, ordinate per <c>Ordine</c>.</summary>
    Task<IReadOnlyList<Menu>> GetMenusAsync(CancellationToken cancellationToken = default);

    /// <summary>Tutte le sottovoci, ordinate per <c>Ordine</c>.</summary>
    Task<IReadOnlyList<SottoMenu>> GetSottoMenusAsync(CancellationToken cancellationToken = default);

    // --- Mapping per RUOLO (matrice ruolo×menu, T3c) ---

    /// <summary>Id dei Menu mappati al ruolo.</summary>
    Task<IReadOnlySet<Guid>> GetMenuRuoloIdsAsync(Guid idRuolo, CancellationToken cancellationToken = default);

    /// <summary>Id dei SottoMenu mappati al ruolo.</summary>
    Task<IReadOnlySet<Guid>> GetSottoMenuRuoloIdsAsync(Guid idRuolo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sostituisce integralmente il mapping di visibilità del ruolo: cancella
    /// le righe MenuRuolo/SottoMenuRuolo esistenti e reinserisce quelle indicate.
    /// </summary>
    Task SetMappingRuoloAsync(Guid idRuolo, IReadOnlyCollection<Guid> menuIds, IReadOnlyCollection<Guid> sottoMenuIds, CancellationToken cancellationToken = default);

    // --- Mapping per UTENTE (override, T3d): quando presente SOSTITUISCE il ruolo ---

    /// <summary>Id dei Menu mappati direttamente all'utente (override).</summary>
    Task<IReadOnlySet<Guid>> GetMenuUtenteIdsAsync(Guid idUtente, CancellationToken cancellationToken = default);

    /// <summary>Id dei SottoMenu mappati direttamente all'utente (override).</summary>
    Task<IReadOnlySet<Guid>> GetSottoMenuUtenteIdsAsync(Guid idUtente, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sostituisce integralmente l'override dell'utente. Passare insiemi vuoti
    /// rimuove la personalizzazione (l'utente torna a seguire il ruolo).
    /// </summary>
    Task SetMappingUtenteAsync(Guid idUtente, IReadOnlyCollection<Guid> menuIds, IReadOnlyCollection<Guid> sottoMenuIds, CancellationToken cancellationToken = default);
}
