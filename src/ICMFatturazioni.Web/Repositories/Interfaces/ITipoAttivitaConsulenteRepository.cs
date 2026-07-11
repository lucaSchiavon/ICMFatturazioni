using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Repositories.Interfaces;

public interface ITipoAttivitaConsulenteRepository
{
    Task<IReadOnlyList<TipoAttivitaConsulente>> GetAttiviAsync(CancellationToken cancellationToken = default);
    Task<TipoAttivitaConsulente?> GetByIdAsync(Guid idTipoAttivitaConsulente, CancellationToken cancellationToken = default);
    Task<bool> ExistsDescrizioneAttivaAsync(string descrizione, Guid? escludiId, CancellationToken cancellationToken = default);
    Task InsertAsync(TipoAttivitaConsulente tipo, CancellationToken cancellationToken = default);
    Task UpdateAsync(TipoAttivitaConsulente tipo, CancellationToken cancellationToken = default);
    Task DisattivaAsync(Guid idTipoAttivitaConsulente, CancellationToken cancellationToken = default);

    /// <summary>Verifica se il tipo è ancora referenziato da righe consulenza attive (fatt.AttivitaConsulenti).</summary>
    Task<bool> HasDipendenzeAsync(Guid idTipoAttivitaConsulente, CancellationToken cancellationToken = default);
}
