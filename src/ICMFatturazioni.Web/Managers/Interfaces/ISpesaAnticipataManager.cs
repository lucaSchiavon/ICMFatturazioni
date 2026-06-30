using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Managers.Interfaces;

/// <summary>
/// Orchestrazione business per le spese anticipate dello studio (dispensa cap. 2).
/// Ogni scrittura è tracciata in <c>fatt.Audit</c> (Regola 7, CLAUDE.md).
/// </summary>
public interface ISpesaAnticipataManager
{
    /// <summary>Restituisce le spese attive di un'attività, ordinate per Data ASC.</summary>
    Task<IReadOnlyList<SpesaAnticipata>> ElencoPerAttivitaAsync(Guid idAttivita, CancellationToken ct = default);

    /// <summary>
    /// Crea una nuova spesa anticipata. Assegna GUID v7.
    /// Lancia <see cref="SpesaAnticipataInvalidaException"/> se la validazione fallisce.
    /// </summary>
    Task<Guid> CreaAsync(SpesaAnticipata spesa, CancellationToken ct = default);

    /// <summary>
    /// Aggiorna una spesa anticipata esistente.
    /// Lancia <see cref="SpesaAnticipataInvalidaException"/> se la validazione fallisce.
    /// </summary>
    Task AggiornaAsync(SpesaAnticipata spesa, CancellationToken ct = default);

    /// <summary>Soft-delete della spesa anticipata.</summary>
    Task EliminaAsync(Guid idSpesaAnticipata, CancellationToken ct = default);
}
