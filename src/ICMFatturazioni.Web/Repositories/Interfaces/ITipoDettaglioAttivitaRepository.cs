using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Repositories.Interfaces;

public interface ITipoDettaglioAttivitaRepository
{
    Task<IReadOnlyList<TipoDettaglioAttivita>> GetAttiviAsync(CancellationToken cancellationToken = default);
    Task<TipoDettaglioAttivita?> GetByIdAsync(Guid idTipoDettaglioAttivita, CancellationToken cancellationToken = default);
    Task<bool> ExistsDescrizioneAttivaAsync(string descrizione, Guid? escludiId, CancellationToken cancellationToken = default);
    Task InsertAsync(TipoDettaglioAttivita tipo, CancellationToken cancellationToken = default);
    Task UpdateAsync(TipoDettaglioAttivita tipo, CancellationToken cancellationToken = default);
    Task DisattivaAsync(Guid idTipoDettaglioAttivita, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se il tipo è referenziato da righe di dettaglio attività attive.
    /// Dipende da fatt.AttivitaDettaglio (migration 027).
    /// </summary>
    Task<bool> HasDipendenzeAsync(Guid idTipoDettaglioAttivita, CancellationToken cancellationToken = default);
}
