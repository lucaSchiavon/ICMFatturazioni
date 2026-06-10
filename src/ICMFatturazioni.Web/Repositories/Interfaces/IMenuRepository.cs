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

    /// <summary>
    /// Id dei <c>Menu</c> visibili per l'utente: unione del mapping di ruolo
    /// (MenuRuolo) e dell'override per utente (MenuUtente).
    /// </summary>
    Task<IReadOnlySet<Guid>> GetIdMenuVisibiliAsync(Guid idRuolo, Guid idUtente, CancellationToken cancellationToken = default);

    /// <summary>
    /// Id dei <c>SottoMenu</c> visibili per l'utente: unione di SottoMenuRuolo
    /// e SottoMenuUtente.
    /// </summary>
    Task<IReadOnlySet<Guid>> GetIdSottoMenuVisibiliAsync(Guid idRuolo, Guid idUtente, CancellationToken cancellationToken = default);

    // --- Configurazione mapping per RUOLO (matrice ruolo×menu, T3c) ---

    /// <summary>Id dei Menu mappati al ruolo (solo MenuRuolo, no override utente).</summary>
    Task<IReadOnlySet<Guid>> GetMenuRuoloIdsAsync(Guid idRuolo, CancellationToken cancellationToken = default);

    /// <summary>Id dei SottoMenu mappati al ruolo (solo SottoMenuRuolo).</summary>
    Task<IReadOnlySet<Guid>> GetSottoMenuRuoloIdsAsync(Guid idRuolo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sostituisce integralmente il mapping di visibilità del ruolo: cancella
    /// le righe MenuRuolo/SottoMenuRuolo esistenti e reinserisce quelle indicate.
    /// </summary>
    Task SetMappingRuoloAsync(Guid idRuolo, IReadOnlyCollection<Guid> menuIds, IReadOnlyCollection<Guid> sottoMenuIds, CancellationToken cancellationToken = default);
}
