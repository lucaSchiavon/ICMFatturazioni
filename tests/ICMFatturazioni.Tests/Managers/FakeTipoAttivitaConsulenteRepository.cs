using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>In-memory di <see cref="ITipoAttivitaConsulenteRepository"/> per i test (no DB).</summary>
internal sealed class FakeTipoAttivitaConsulenteRepository : ITipoAttivitaConsulenteRepository
{
    private readonly Dictionary<Guid, TipoAttivitaConsulente> _store = new();

    /// <summary>Id che il fake dichiarerà "con dipendenze" (righe consulenza figlie).</summary>
    public HashSet<Guid> DipendenzeDa { get; } = new();

    public Task<IReadOnlyList<TipoAttivitaConsulente>> GetAttiviAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TipoAttivitaConsulente>>(
            _store.Values.Where(t => t.IsAttivo).OrderBy(t => t.Descrizione, StringComparer.OrdinalIgnoreCase).ToList());

    public Task<TipoAttivitaConsulente?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue(id, out var t) ? t : null);

    public Task<bool> ExistsDescrizioneAttivaAsync(string descrizione, Guid? escludiId, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.Values.Any(t =>
            t.IsAttivo
            && string.Equals(t.Descrizione, descrizione, StringComparison.OrdinalIgnoreCase)
            && (escludiId is null || t.IdTipoAttivitaConsulente != escludiId)));

    public Task InsertAsync(TipoAttivitaConsulente tipo, CancellationToken cancellationToken = default)
    {
        _store[tipo.IdTipoAttivitaConsulente] = tipo;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(TipoAttivitaConsulente tipo, CancellationToken cancellationToken = default)
    {
        _store[tipo.IdTipoAttivitaConsulente] = tipo;
        return Task.CompletedTask;
    }

    public Task DisattivaAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(id, out var t))
        {
            _store[id] = new TipoAttivitaConsulente
            {
                IdTipoAttivitaConsulente = t.IdTipoAttivitaConsulente,
                Descrizione              = t.Descrizione,
                IsAttivo                 = false,
            };
        }
        return Task.CompletedTask;
    }

    public Task<bool> HasDipendenzeAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(DipendenzeDa.Contains(id));
}
