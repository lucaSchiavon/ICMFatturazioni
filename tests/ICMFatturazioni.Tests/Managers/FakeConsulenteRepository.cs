using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>In-memory di <see cref="IConsulenteRepository"/> per i test (no DB).</summary>
internal sealed class FakeConsulenteRepository : IConsulenteRepository
{
    private readonly Dictionary<Guid, Consulente> _store = new();

    /// <summary>Id che il fake dichiarerà "con dipendenze" (righe consulenza figlie).</summary>
    public HashSet<Guid> DipendenzeDa { get; } = new();

    public Task<IReadOnlyList<Consulente>> GetAttiviAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Consulente>>(
            _store.Values.Where(c => c.IsAttivo).OrderBy(c => c.Descrizione, StringComparer.OrdinalIgnoreCase).ToList());

    public Task<Consulente?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue(id, out var c) ? c : null);

    public Task<bool> ExistsDescrizioneAttivaAsync(string descrizione, Guid? escludiId, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.Values.Any(c =>
            c.IsAttivo
            && string.Equals(c.Descrizione, descrizione, StringComparison.OrdinalIgnoreCase)
            && (escludiId is null || c.IdConsulente != escludiId)));

    public Task InsertAsync(Consulente consulente, CancellationToken cancellationToken = default)
    {
        _store[consulente.IdConsulente] = consulente;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Consulente consulente, CancellationToken cancellationToken = default)
    {
        _store[consulente.IdConsulente] = consulente;
        return Task.CompletedTask;
    }

    public Task DisattivaAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(id, out var c))
        {
            _store[id] = new Consulente
            {
                IdConsulente = c.IdConsulente,
                Descrizione  = c.Descrizione,
                IsAttivo     = false,
            };
        }
        return Task.CompletedTask;
    }

    public Task<bool> HasDipendenzeAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(DipendenzeDa.Contains(id));
}
