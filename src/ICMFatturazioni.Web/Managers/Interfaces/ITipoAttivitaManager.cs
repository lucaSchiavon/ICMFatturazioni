using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Managers.Interfaces;

public interface ITipoAttivitaManager
{
    Task<IReadOnlyList<TipoAttivita>> ElencoAsync(CancellationToken cancellationToken = default);
    Task<TipoAttivita?> GetByIdAsync(Guid idTipoAttivita, CancellationToken cancellationToken = default);
    Task<Guid> CreaAsync(TipoAttivita tipo, CancellationToken cancellationToken = default);
    Task AggiornaAsync(TipoAttivita tipo, CancellationToken cancellationToken = default);
    Task EliminaAsync(Guid idTipoAttivita, CancellationToken cancellationToken = default);
    Task<bool> EEliminabileAsync(Guid idTipoAttivita, CancellationToken cancellationToken = default);
}
