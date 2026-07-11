using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Web.Repositories.Interfaces;

public interface IAttivitaConsulenteRepository
{
    /// <summary>Righe consulenza attive di un'attività, con descrizioni di consulente e tipo (join).</summary>
    Task<IReadOnlyList<AttivitaConsulente>> GetByAttivitaAsync(Guid idAttivita, CancellationToken cancellationToken = default);

    /// <summary>
    /// Scheda del consulente (dispensa cap. 6): TUTTE le sue righe consulenza attive
    /// su tutti i clienti, con cliente/attività/tipo e Pagato derivato dalle tranche.
    /// I raffinamenti (anagrafica, attività, stato D-C4) si applicano in memoria.
    /// </summary>
    Task<IReadOnlyList<SchedaConsulenzaRiga>> GetSchedaConsulenteAsync(Guid idConsulente, CancellationToken cancellationToken = default);

    Task<AttivitaConsulente?> GetByIdAsync(Guid idAttivitaConsulente, CancellationToken cancellationToken = default);
    Task InsertAsync(AttivitaConsulente riga, CancellationToken cancellationToken = default);
    Task UpdateAsync(AttivitaConsulente riga, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-delete con sentinel (D-C2): la UPDATE non tocca la riga se esistono
    /// tranche di pagamento attive — anche se il pre-check del manager fosse aggirato.
    /// </summary>
    Task DisattivaAsync(Guid idAttivitaConsulente, CancellationToken cancellationToken = default);

    /// <summary>Verifica se la riga ha tranche di pagamento attive (pre-check D-C2).</summary>
    Task<bool> HasPagamentiAsync(Guid idAttivitaConsulente, CancellationToken cancellationToken = default);

    /// <summary>Somma delle tranche di pagamento attive della riga (0 se nessuna).</summary>
    Task<decimal> GetPagatoAsync(Guid idAttivitaConsulente, CancellationToken cancellationToken = default);
}
