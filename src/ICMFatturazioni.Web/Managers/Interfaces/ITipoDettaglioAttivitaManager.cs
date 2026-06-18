using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Managers.Interfaces;

public interface ITipoDettaglioAttivitaManager
{
    Task<IReadOnlyList<TipoDettaglioAttivita>> ElencoAsync(CancellationToken cancellationToken = default);
    Task<TipoDettaglioAttivita?> GetByIdAsync(Guid idTipoDettaglioAttivita, CancellationToken cancellationToken = default);
    Task<Guid> CreaAsync(TipoDettaglioAttivita tipo, CancellationToken cancellationToken = default);
    Task AggiornaAsync(TipoDettaglioAttivita tipo, CancellationToken cancellationToken = default);
    Task EliminaAsync(Guid idTipoDettaglioAttivita, CancellationToken cancellationToken = default);
    Task<bool> EEliminabileAsync(Guid idTipoDettaglioAttivita, CancellationToken cancellationToken = default);
}
