using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>In-memory di <see cref="IDescrizioneAttivitaRepository"/> per i test (no DB).</summary>
internal sealed class FakeDescrizioneAttivitaRepository : IDescrizioneAttivitaRepository
{
    private readonly Dictionary<Guid, DescrizioneAttivita> _store = new();

    public Task<IReadOnlyList<DescrizioneAttivita>> GetAttiviAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<DescrizioneAttivita>>(
            _store.Values.Where(d => d.IsAttivo)
                .OrderBy(d => d.Ordine)
                .ThenBy(d => d.Descrizione, StringComparer.OrdinalIgnoreCase)
                .ToList());

    public Task<DescrizioneAttivita?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue(id, out var d) ? d : null);

    public Task<bool> ExistsDescrizioneAttivaAsync(string descrizione, Guid? escludiId, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.Values.Any(d =>
            d.IsAttivo
            && string.Equals(d.Descrizione, descrizione, StringComparison.OrdinalIgnoreCase)
            && (escludiId is null || d.IdDescrizioneAttivita != escludiId)));

    public Task InsertAsync(DescrizioneAttivita desc, CancellationToken cancellationToken = default)
    {
        _store[desc.IdDescrizioneAttivita] = desc;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(DescrizioneAttivita desc, CancellationToken cancellationToken = default)
    {
        _store[desc.IdDescrizioneAttivita] = desc;
        return Task.CompletedTask;
    }

    public Task DisattivaAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(id, out var d))
        {
            _store[id] = new DescrizioneAttivita
            {
                IdDescrizioneAttivita = d.IdDescrizioneAttivita,
                Descrizione           = d.Descrizione,
                Ordine                = d.Ordine,
                IsAttivo              = false,
            };
        }
        return Task.CompletedTask;
    }
}
