using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Managers.Interfaces;

public interface ITipoAttivitaConsulenteManager
{
    Task<IReadOnlyList<TipoAttivitaConsulente>> ElencoAsync(CancellationToken cancellationToken = default);
    Task<TipoAttivitaConsulente?> GetByIdAsync(Guid idTipoAttivitaConsulente, CancellationToken cancellationToken = default);
    Task<Guid> CreaAsync(TipoAttivitaConsulente tipo, CancellationToken cancellationToken = default);
    Task AggiornaAsync(TipoAttivitaConsulente tipo, CancellationToken cancellationToken = default);
    Task EliminaAsync(Guid idTipoAttivitaConsulente, CancellationToken cancellationToken = default);
    Task<bool> EEliminabileAsync(Guid idTipoAttivitaConsulente, CancellationToken cancellationToken = default);
}
