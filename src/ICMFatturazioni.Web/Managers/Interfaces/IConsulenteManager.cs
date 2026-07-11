using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Managers.Interfaces;

public interface IConsulenteManager
{
    Task<IReadOnlyList<Consulente>> ElencoAsync(CancellationToken cancellationToken = default);
    Task<Consulente?> GetByIdAsync(Guid idConsulente, CancellationToken cancellationToken = default);
    Task<Guid> CreaAsync(Consulente consulente, CancellationToken cancellationToken = default);
    Task AggiornaAsync(Consulente consulente, CancellationToken cancellationToken = default);
    Task EliminaAsync(Guid idConsulente, CancellationToken cancellationToken = default);
    Task<bool> EEliminabileAsync(Guid idConsulente, CancellationToken cancellationToken = default);
}
