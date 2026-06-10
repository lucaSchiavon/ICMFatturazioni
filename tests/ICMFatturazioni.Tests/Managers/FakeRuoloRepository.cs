using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Doppio in-memory di <see cref="IRuoloRepository"/> per i test del
/// <c>RuoloManager</c>. Il conteggio utenti è pilotabile via
/// <see cref="UtentiPerRuolo"/> per provare il rifiuto di eliminazione di un
/// ruolo ancora in uso.
/// </summary>
internal sealed class FakeRuoloRepository : IRuoloRepository
{
    private readonly Dictionary<Guid, Ruolo> _store = new();

    /// <summary>Numero di utenti assegnati a un ruolo (default 0).</summary>
    public Dictionary<Guid, int> UtentiPerRuolo { get; } = new();

    public IReadOnlyDictionary<Guid, Ruolo> Store => _store;

    /// <summary>Helper per pre-popolare lo store nei test (es. ruoli di sistema).</summary>
    public Ruolo Seed(Ruolo ruolo)
    {
        _store[ruolo.IdRuolo] = ruolo;
        return ruolo;
    }

    public Task<IReadOnlyList<Ruolo>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Ruolo> list = _store.Values
            .OrderByDescending(r => r.IsSistema)
            .ThenBy(r => r.Nome, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return Task.FromResult(list);
    }

    public Task<Ruolo?> GetByIdAsync(Guid idRuolo, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue(idRuolo, out var r) ? r : null);

    public Task<Ruolo?> GetByCodiceAsync(string codice, CancellationToken cancellationToken = default)
    {
        var r = _store.Values.FirstOrDefault(
            x => string.Equals(x.Codice, codice, StringComparison.Ordinal));
        return Task.FromResult<Ruolo?>(r);
    }

    public Task<bool> ExistsNomeAsync(string nome, Guid? escludiIdRuolo = null, CancellationToken cancellationToken = default)
    {
        var exists = _store.Values.Any(x =>
            string.Equals(x.Nome, nome.Trim(), StringComparison.OrdinalIgnoreCase)
            && (escludiIdRuolo is null || x.IdRuolo != escludiIdRuolo.Value));
        return Task.FromResult(exists);
    }

    public Task<int> CountUtentiAsync(Guid idRuolo, CancellationToken cancellationToken = default)
        => Task.FromResult(UtentiPerRuolo.TryGetValue(idRuolo, out var n) ? n : 0);

    public Task InsertAsync(Ruolo ruolo, CancellationToken cancellationToken = default)
    {
        _store[ruolo.IdRuolo] = ruolo;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Guid idRuolo, string nome, string? descrizione, bool isAttivo, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(idRuolo, out var r))
        {
            _store[idRuolo] = new Ruolo
            {
                IdRuolo = r.IdRuolo,
                Codice = r.Codice,
                Nome = nome,
                Descrizione = descrizione,
                IsSistema = r.IsSistema,
                IsAttivo = isAttivo,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt,
            };
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid idRuolo, CancellationToken cancellationToken = default)
    {
        _store.Remove(idRuolo);
        return Task.CompletedTask;
    }
}
