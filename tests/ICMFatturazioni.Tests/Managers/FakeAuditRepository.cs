using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Models;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Doppio in-memory di <see cref="IAuditRepository"/>. Può simulare un errore
/// di scrittura (<see cref="ThrowOnInsert"/>) per verificare che l'audit sia
/// best-effort e non propaghi il fallimento.
/// </summary>
internal sealed class FakeAuditRepository : IAuditRepository
{
    public List<Audit> Inseriti { get; } = new();
    public bool ThrowOnInsert { get; set; }

    public Task InsertAsync(Audit entry, CancellationToken cancellationToken = default)
    {
        if (ThrowOnInsert)
        {
            throw new InvalidOperationException("DB irraggiungibile (simulato).");
        }
        Inseriti.Add(entry);
        return Task.CompletedTask;
    }

    public Task<AuditRisultato> CercaAsync(AuditFiltro filtro, CancellationToken cancellationToken = default)
    {
        IEnumerable<Audit> q = Inseriti;
        if (filtro.DaUtc is { } da) q = q.Where(a => a.TimestampUtc >= da);
        if (filtro.AUtc is { } au) q = q.Where(a => a.TimestampUtc < au);
        if (filtro.Operazione is { } op) q = q.Where(a => a.Operazione == op);
        if (!string.IsNullOrWhiteSpace(filtro.EntityType))
            q = q.Where(a => a.EntityType == filtro.EntityType);
        if (!string.IsNullOrWhiteSpace(filtro.Testo))
            q = q.Where(a => (a.UtenteNome?.Contains(filtro.Testo, StringComparison.OrdinalIgnoreCase) ?? false)
                || (a.Descrizione?.Contains(filtro.Testo, StringComparison.OrdinalIgnoreCase) ?? false));

        var ordinati = q.OrderByDescending(a => a.TimestampUtc).ToList();
        var pagina = filtro.Pagina < 1 ? 1 : filtro.Pagina;
        var dim = filtro.Dimensione is < 1 or > 200 ? 25 : filtro.Dimensione;
        var righe = ordinati.Skip((pagina - 1) * dim).Take(dim).ToList();
        return Task.FromResult(new AuditRisultato(righe, ordinati.Count));
    }

    public Task<IReadOnlyList<string>> GetEntityTypesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(
            Inseriti.Select(a => a.EntityType).Distinct().OrderBy(t => t).ToList());

    public Task<int> PurgaPrecedentiAsync(DateTime sogliaUtc, CancellationToken cancellationToken = default)
    {
        var rimossi = Inseriti.RemoveAll(a => a.TimestampUtc < sogliaUtc);
        return Task.FromResult(rimossi);
    }
}
