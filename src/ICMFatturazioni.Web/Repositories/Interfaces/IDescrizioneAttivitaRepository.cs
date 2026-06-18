using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Repositories.Interfaces;

public interface IDescrizioneAttivitaRepository
{
    Task<IReadOnlyList<DescrizioneAttivita>> GetAttiviAsync(CancellationToken cancellationToken = default);
    Task<DescrizioneAttivita?> GetByIdAsync(Guid idDescrizioneAttivita, CancellationToken cancellationToken = default);
    Task<bool> ExistsDescrizioneAttivaAsync(string descrizione, Guid? escludiId, CancellationToken cancellationToken = default);
    Task InsertAsync(DescrizioneAttivita descrizione, CancellationToken cancellationToken = default);
    Task UpdateAsync(DescrizioneAttivita descrizione, CancellationToken cancellationToken = default);
    Task DisattivaAsync(Guid idDescrizioneAttivita, CancellationToken cancellationToken = default);
}
