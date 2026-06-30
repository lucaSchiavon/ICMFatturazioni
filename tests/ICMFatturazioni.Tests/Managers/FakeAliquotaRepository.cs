using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>Implementazione in-memory di <see cref="IAliquotaRepository"/> per i test.</summary>
internal sealed class FakeAliquotaRepository : IAliquotaRepository
{
    private readonly List<Aliquota> _store = new();

    /// <summary>Helper per pre-popolare un'aliquota (es. di sistema).</summary>
    public FakeAliquotaRepository Con(string? codice, string descrizione, decimal valore)
    {
        _store.Add(new Aliquota
        {
            IdAliquota  = Guid.NewGuid(),
            Codice      = codice,
            Descrizione = descrizione,
            Valore      = valore,
        });
        return this;
    }

    public Task<IReadOnlyList<Aliquota>> GetAttiviAsync(CancellationToken ct = default)
    {
        var result = _store.Where(a => a.IsAttivo).OrderBy(a => a.Descrizione).ToList();
        return Task.FromResult<IReadOnlyList<Aliquota>>(result);
    }

    public Task<Aliquota?> GetByIdAsync(Guid idAliquota, CancellationToken ct = default)
        => Task.FromResult(_store.FirstOrDefault(a => a.IdAliquota == idAliquota));

    public Task InsertAsync(Aliquota aliquota, CancellationToken ct = default)
    {
        _store.Add(aliquota);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Aliquota aliquota, CancellationToken ct = default)
    {
        var idx = _store.FindIndex(a => a.IdAliquota == aliquota.IdAliquota);
        if (idx >= 0)
            _store[idx] = new Aliquota
            {
                IdAliquota  = aliquota.IdAliquota,
                Codice      = _store[idx].Codice,   // Codice non aggiornabile
                Descrizione = aliquota.Descrizione,
                Valore      = aliquota.Valore,
                IsAttivo    = _store[idx].IsAttivo,
            };
        return Task.CompletedTask;
    }

    public Task DisattivaAsync(Guid idAliquota, CancellationToken ct = default)
    {
        var idx = _store.FindIndex(a => a.IdAliquota == idAliquota);
        if (idx >= 0)
        {
            var a = _store[idx];
            _store[idx] = new Aliquota
            {
                IdAliquota  = a.IdAliquota,
                Codice      = a.Codice,
                Descrizione = a.Descrizione,
                Valore      = a.Valore,
                IsAttivo    = false,
            };
        }
        return Task.CompletedTask;
    }
}
