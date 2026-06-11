using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Web.Repositories.Interfaces;

/// <summary>
/// Accesso dati a <c>fatt.Log</c>: scrittura (immutabile, solo INSERT), ricerca
/// paginata per la pagina <c>/admin/log</c> e purga dei log più vecchi.
/// </summary>
public interface ILogRepository
{
    /// <summary>Inserisce un singolo evento (path esplicito di <c>LogManager</c>).</summary>
    Task InsertAsync(Log entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserimento a batch, usato dal <c>LogWriterService</c> che drena la coda.
    /// Lascia propagare le eccezioni: il fallback (file/console) è gestito dal
    /// chiamante.
    /// </summary>
    Task InsertBatchAsync(IReadOnlyList<Log> entries, CancellationToken cancellationToken = default);

    /// <summary>Ricerca paginata, ordinata per timestamp decrescente.</summary>
    Task<LogRisultato> CercaAsync(LogFiltro filtro, CancellationToken cancellationToken = default);

    /// <summary>Elimina le righe con <c>TimestampUtc &lt; soglia</c>. Ritorna le righe eliminate.</summary>
    Task<int> PurgaPrecedentiAsync(DateTime sogliaUtc, CancellationToken cancellationToken = default);
}
