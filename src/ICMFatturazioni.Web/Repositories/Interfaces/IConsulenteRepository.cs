using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Repositories.Interfaces;

public interface IConsulenteRepository
{
    Task<IReadOnlyList<Consulente>> GetAttiviAsync(CancellationToken cancellationToken = default);
    Task<Consulente?> GetByIdAsync(Guid idConsulente, CancellationToken cancellationToken = default);
    Task<bool> ExistsDescrizioneAttivaAsync(string descrizione, Guid? escludiId, CancellationToken cancellationToken = default);
    Task InsertAsync(Consulente consulente, CancellationToken cancellationToken = default);
    Task UpdateAsync(Consulente consulente, CancellationToken cancellationToken = default);
    Task DisattivaAsync(Guid idConsulente, CancellationToken cancellationToken = default);

    /// <summary>Verifica se il consulente è ancora referenziato da righe consulenza attive (fatt.AttivitaConsulenti).</summary>
    Task<bool> HasDipendenzeAsync(Guid idConsulente, CancellationToken cancellationToken = default);
}
