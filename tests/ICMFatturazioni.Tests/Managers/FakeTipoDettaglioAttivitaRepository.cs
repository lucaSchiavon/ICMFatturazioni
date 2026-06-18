using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>In-memory di <see cref="ITipoDettaglioAttivitaRepository"/> per i test (no DB).</summary>
internal sealed class FakeTipoDettaglioAttivitaRepository : ITipoDettaglioAttivitaRepository
{
    private readonly Dictionary<Guid, TipoDettaglioAttivita> _store = new();

    /// <summary>Id che il fake dichiarerà "con dipendenze" (dettagli attività figli).</summary>
    public HashSet<Guid> DipendenzeDa { get; } = new();

    public Task<IReadOnlyList<TipoDettaglioAttivita>> GetAttiviAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TipoDettaglioAttivita>>(
            _store.Values.Where(t => t.IsAttivo).OrderBy(t => t.Descrizione, StringComparer.OrdinalIgnoreCase).ToList());

    public Task<TipoDettaglioAttivita?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue(id, out var t) ? t : null);

    public Task<bool> ExistsDescrizioneAttivaAsync(string descrizione, Guid? escludiId, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.Values.Any(t =>
            t.IsAttivo
            && string.Equals(t.Descrizione, descrizione, StringComparison.OrdinalIgnoreCase)
            && (escludiId is null || t.IdTipoDettaglioAttivita != escludiId)));

    public Task InsertAsync(TipoDettaglioAttivita tipo, CancellationToken cancellationToken = default)
    {
        _store[tipo.IdTipoDettaglioAttivita] = tipo;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(TipoDettaglioAttivita tipo, CancellationToken cancellationToken = default)
    {
        _store[tipo.IdTipoDettaglioAttivita] = tipo;
        return Task.CompletedTask;
    }

    public Task DisattivaAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(id, out var t))
        {
            _store[id] = new TipoDettaglioAttivita
            {
                IdTipoDettaglioAttivita = t.IdTipoDettaglioAttivita,
                Descrizione             = t.Descrizione,
                IsAttivo                = false,
            };
        }
        return Task.CompletedTask;
    }

    public Task<bool> HasDipendenzeAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(DipendenzeDa.Contains(id));
}
