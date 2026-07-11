using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Web.Managers.Interfaces;

public interface IAttivitaConsulenteManager
{
    /// <summary>Righe consulenza attive di un'attività (descrizioni consulente/tipo popolate).</summary>
    Task<IReadOnlyList<AttivitaConsulente>> ElencoPerAttivitaAsync(Guid idAttivita, CancellationToken cancellationToken = default);

    /// <summary>Scheda del consulente: tutte le sue consulenze attive su tutti i clienti (dispensa cap. 6).</summary>
    Task<IReadOnlyList<SchedaConsulenzaRiga>> SchedaConsulenteAsync(Guid idConsulente, CancellationToken cancellationToken = default);

    /// <summary>Scheda generale: le consulenze attive di TUTTI i consulenti (variante generale del report, dispensa cap. 7).</summary>
    Task<IReadOnlyList<SchedaConsulenzaRiga>> SchedaGeneraleAsync(CancellationToken cancellationToken = default);

    Task<Guid> CreaAsync(AttivitaConsulente riga, CancellationToken cancellationToken = default);
    Task AggiornaAsync(AttivitaConsulente riga, CancellationToken cancellationToken = default);

    /// <summary>Soft-delete. Bloccata se la riga ha pagamenti attivi (D-C2).</summary>
    Task EliminaAsync(Guid idAttivitaConsulente, CancellationToken cancellationToken = default);
}
