using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Managers.Interfaces;

public interface IAttivitaConsulenteManager
{
    /// <summary>Righe consulenza attive di un'attività (descrizioni consulente/tipo popolate).</summary>
    Task<IReadOnlyList<AttivitaConsulente>> ElencoPerAttivitaAsync(Guid idAttivita, CancellationToken cancellationToken = default);

    Task<Guid> CreaAsync(AttivitaConsulente riga, CancellationToken cancellationToken = default);
    Task AggiornaAsync(AttivitaConsulente riga, CancellationToken cancellationToken = default);

    /// <summary>Soft-delete. Bloccata se la riga ha pagamenti attivi (D-C2).</summary>
    Task EliminaAsync(Guid idAttivitaConsulente, CancellationToken cancellationToken = default);
}
