using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Repositories.Interfaces;

/// <summary>
/// Accesso ai dati per <c>fatt.SpeseAnticipate</c>.
/// Ordinamento implicito delle letture per attività: <c>Data ASC</c>.
/// </summary>
public interface ISpesaAnticipataRepository
{
    /// <summary>Restituisce le spese attive di un'attività, ordinate per Data ASC.</summary>
    Task<IReadOnlyList<SpesaAnticipata>> GetByAttivitaAsync(Guid idAttivita, CancellationToken ct = default);

    /// <summary>
    /// Restituisce le spese attive di un'attività <b>ancora fatturabili</b>, cioè
    /// non ancora collegate ad alcun avviso (<c>IdAvviso IS NULL</c>). Ordinate per
    /// Data ASC. Usata dalla maschera Avvisi per proporre solo le spese non già
    /// riaddebitate in un altro avviso.
    /// </summary>
    Task<IReadOnlyList<SpesaAnticipata>> GetFatturabiliByAttivitaAsync(Guid idAttivita, CancellationToken ct = default);

    /// <summary>
    /// Restituisce le spese attive collegate a un avviso (<c>IdAvviso = @IdAvviso</c>),
    /// ordinate per Data ASC. Usata per riaddebitare le spese art.15 di un avviso
    /// emesso (es. cascata fiscale del PDF).
    /// </summary>
    Task<IReadOnlyList<SpesaAnticipata>> GetByAvvisoAsync(Guid idAvviso, CancellationToken ct = default);

    /// <summary>Restituisce una spesa per chiave primaria (incluse soft-deleted).</summary>
    Task<SpesaAnticipata?> GetByIdAsync(Guid idSpesaAnticipata, CancellationToken ct = default);

    /// <summary>Inserisce una nuova spesa anticipata.</summary>
    Task InsertAsync(SpesaAnticipata spesa, CancellationToken ct = default);

    /// <summary>Aggiorna una spesa anticipata esistente.</summary>
    Task UpdateAsync(SpesaAnticipata spesa, CancellationToken ct = default);

    /// <summary>Soft-delete: imposta IsAttivo = 0.</summary>
    Task DisattivaAsync(Guid idSpesaAnticipata, CancellationToken ct = default);
}
