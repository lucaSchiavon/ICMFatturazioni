using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Implementazione in-memory di <see cref="ISpesaAnticipataRepository"/> per i test unitari.
/// </summary>
internal sealed class FakeSpesaAnticipataRepository : ISpesaAnticipataRepository
{
    private readonly List<SpesaAnticipata> _store = new();

    public Task<IReadOnlyList<SpesaAnticipata>> GetByAttivitaAsync(Guid idAttivita, CancellationToken ct = default)
    {
        var result = _store
            .Where(s => s.IdAttivita == idAttivita && s.IsAttivo)
            .OrderBy(s => s.Data)
            .ToList();
        return Task.FromResult<IReadOnlyList<SpesaAnticipata>>(result);
    }

    /// <summary>Id delle spese "già collegate" a un avviso (escluse dai fatturabili).</summary>
    public HashSet<Guid> Collegate { get; } = new();

    public Task<IReadOnlyList<SpesaAnticipata>> GetFatturabiliByAttivitaAsync(Guid idAttivita, CancellationToken ct = default)
    {
        var result = _store
            .Where(s => s.IdAttivita == idAttivita && s.IsAttivo && !Collegate.Contains(s.IdSpesaAnticipata))
            .OrderBy(s => s.Data)
            .ToList();
        return Task.FromResult<IReadOnlyList<SpesaAnticipata>>(result);
    }

    public Task<SpesaAnticipata?> GetByIdAsync(Guid idSpesaAnticipata, CancellationToken ct = default)
    {
        var s = _store.FirstOrDefault(x => x.IdSpesaAnticipata == idSpesaAnticipata);
        return Task.FromResult<SpesaAnticipata?>(s);
    }

    public Task InsertAsync(SpesaAnticipata spesa, CancellationToken ct = default)
    {
        _store.Add(spesa);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(SpesaAnticipata spesa, CancellationToken ct = default)
    {
        var idx = _store.FindIndex(s => s.IdSpesaAnticipata == spesa.IdSpesaAnticipata);
        if (idx >= 0) _store[idx] = spesa;
        return Task.CompletedTask;
    }

    public Task DisattivaAsync(Guid idSpesaAnticipata, CancellationToken ct = default)
    {
        var idx = _store.FindIndex(s => s.IdSpesaAnticipata == idSpesaAnticipata);
        if (idx < 0) return Task.CompletedTask;
        var s = _store[idx];
        _store[idx] = new SpesaAnticipata
        {
            IdSpesaAnticipata = s.IdSpesaAnticipata,
            IdAttivita        = s.IdAttivita,
            Data              = s.Data,
            Descrizione       = s.Descrizione,
            Importo           = s.Importo,
            IsAttivo          = false,
        };
        return Task.CompletedTask;
    }
}
