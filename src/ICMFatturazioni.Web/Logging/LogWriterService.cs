using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Logging;

/// <summary>
/// <see cref="BackgroundService"/> che drena <see cref="ILogQueue"/> e scrive
/// gli eventi su <c>fatt.Log</c> a batch. Unico consumatore della coda, vive
/// per tutta la durata dell'app. Disaccoppia la scrittura su DB dal thread di
/// richiesta: il produttore accoda e prosegue senza attendere l'I/O.
/// </summary>
internal sealed class LogWriterService : BackgroundService
{
    private const int BatchMax = 100;

    private readonly ILogQueue _queue;
    private readonly ILogRepository _repo;
    private readonly ILogFallbackWriter _fallback;

    public LogWriterService(ILogQueue queue, ILogRepository repo, ILogFallbackWriter fallback)
    {
        _queue = queue;
        _repo = repo;
        _fallback = fallback;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = _queue.Reader;
        var batch = new List<Log>(BatchMax);

        while (await reader.WaitToReadAsync(stoppingToken))
        {
            batch.Clear();
            while (batch.Count < BatchMax && reader.TryRead(out var entry))
            {
                batch.Add(entry);
            }

            if (batch.Count == 0)
            {
                continue;
            }

            try
            {
                await _repo.InsertBatchAsync(batch, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // DB irraggiungibile (lo scenario che più conta tracciare):
                // riversiamo ogni evento del batch sui sink di fallback
                // (Console.Error + file). NON usiamo mai ILogger qui, o gli
                // eventi rientrerebbero in coda all'infinito.
                foreach (var entry in batch)
                {
                    _fallback.Write(entry, ex);
                }
            }
        }
    }
}
