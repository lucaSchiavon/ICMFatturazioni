using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Repositories.Interfaces;

public interface ITipoAttivitaRepository
{
    Task<IReadOnlyList<TipoAttivita>> GetAttiviAsync(CancellationToken cancellationToken = default);
    Task<TipoAttivita?> GetByIdAsync(Guid idTipoAttivita, CancellationToken cancellationToken = default);
    Task<bool> ExistsDescrizioneAttivaAsync(string descrizione, Guid? escludiId, CancellationToken cancellationToken = default);
    Task InsertAsync(TipoAttivita tipo, CancellationToken cancellationToken = default);
    Task UpdateAsync(TipoAttivita tipo, CancellationToken cancellationToken = default);
    Task DisattivaAsync(Guid idTipoAttivita, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se il tipo è ancora referenziato da attività attive.
    /// Dipende da fatt.Attivita (migration 026): sicuro da chiamare solo dopo quella migration.
    /// </summary>
    Task<bool> HasDipendenzeAsync(Guid idTipoAttivita, CancellationToken cancellationToken = default);
}
