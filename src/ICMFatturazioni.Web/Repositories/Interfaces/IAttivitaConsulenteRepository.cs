using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Web.Repositories.Interfaces;

public interface IAttivitaConsulenteRepository
{
    /// <summary>Righe consulenza attive di un'attività, con descrizioni di consulente e tipo (join).</summary>
    Task<IReadOnlyList<AttivitaConsulente>> GetByAttivitaAsync(Guid idAttivita, CancellationToken cancellationToken = default);

    /// <summary>
    /// Scheda consulenze (dispensa cap. 6-7): righe consulenza attive con
    /// cliente/attività/tipo/consulente e Pagato derivato dalle tranche.
    /// <paramref name="idConsulente"/> null = TUTTI i consulenti (variante
    /// generale del report). I raffinamenti (anagrafica, attività, stato D-C4)
    /// si applicano in memoria.
    /// </summary>
    Task<IReadOnlyList<SchedaConsulenzaRiga>> GetSchedaAsync(Guid? idConsulente, CancellationToken cancellationToken = default);

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
