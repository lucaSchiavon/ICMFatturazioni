using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Models;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Doppio in-memory di <see cref="ILogRepository"/>. Può simulare un DB
/// irraggiungibile (<see cref="ThrowOnInsert"/>) per verificare il fallback.
/// </summary>
internal sealed class FakeLogRepository : ILogRepository
{
    public List<Log> Inseriti { get; } = new();
    public bool ThrowOnInsert { get; set; }

    public Task InsertAsync(Log entry, CancellationToken cancellationToken = default)
    {
        if (ThrowOnInsert)
        {
            throw new InvalidOperationException("DB irraggiungibile (simulato).");
        }
        Inseriti.Add(entry);
        return Task.CompletedTask;
    }

    public Task InsertBatchAsync(IReadOnlyList<Log> entries, CancellationToken cancellationToken = default)
    {
        if (ThrowOnInsert)
        {
            throw new InvalidOperationException("DB irraggiungibile (simulato).");
        }
        Inseriti.AddRange(entries);
        return Task.CompletedTask;
    }

    public Task<LogRisultato> CercaAsync(LogFiltro filtro, CancellationToken cancellationToken = default)
    {
        IEnumerable<Log> q = Inseriti;
        if (filtro.DaUtc is { } da) q = q.Where(l => l.TimestampUtc >= da);
        if (filtro.AUtc is { } a) q = q.Where(l => l.TimestampUtc < a);
        if (filtro.Livello is { } liv) q = q.Where(l => l.Livello == liv);
        if (!string.IsNullOrWhiteSpace(filtro.SorgenteContiene))
            q = q.Where(l => l.Sorgente.Contains(filtro.SorgenteContiene, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filtro.Testo))
            q = q.Where(l => l.Messaggio.Contains(filtro.Testo, StringComparison.OrdinalIgnoreCase)
                || (l.SpiegazioneUtente?.Contains(filtro.Testo, StringComparison.OrdinalIgnoreCase) ?? false));

        var ordinati = q.OrderByDescending(l => l.TimestampUtc).ToList();
        var pagina = filtro.Pagina < 1 ? 1 : filtro.Pagina;
        var dim = filtro.Dimensione is < 1 or > 200 ? 25 : filtro.Dimensione;
        var righe = ordinati.Skip((pagina - 1) * dim).Take(dim).ToList();
        return Task.FromResult(new LogRisultato(righe, ordinati.Count));
    }

    public Task<int> PurgaPrecedentiAsync(DateTime sogliaUtc, CancellationToken cancellationToken = default)
    {
        var rimossi = Inseriti.RemoveAll(l => l.TimestampUtc < sogliaUtc);
        return Task.FromResult(rimossi);
    }
}
