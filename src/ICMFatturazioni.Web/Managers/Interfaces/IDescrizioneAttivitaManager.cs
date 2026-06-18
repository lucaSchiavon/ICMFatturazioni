using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Managers.Interfaces;

public interface IDescrizioneAttivitaManager
{
    Task<IReadOnlyList<DescrizioneAttivita>> ElencoAsync(CancellationToken cancellationToken = default);
    Task<DescrizioneAttivita?> GetByIdAsync(Guid idDescrizioneAttivita, CancellationToken cancellationToken = default);
    Task<Guid> CreaAsync(DescrizioneAttivita descrizione, CancellationToken cancellationToken = default);
    Task AggiornaAsync(DescrizioneAttivita descrizione, CancellationToken cancellationToken = default);
    Task EliminaAsync(Guid idDescrizioneAttivita, CancellationToken cancellationToken = default);
}
