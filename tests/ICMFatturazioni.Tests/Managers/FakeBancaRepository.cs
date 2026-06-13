using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>In-memory di <see cref="IBancaRepository"/> per i test (no DB).</summary>
internal sealed class FakeBancaRepository : IBancaRepository
{
    public Dictionary<Guid, Banca> Store { get; } = new();

    public Task<IReadOnlyList<Banca>> GetAttiveAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Banca>>(
            Store.Values.Where(b => b.IsAttivo).OrderBy(b => b.Nome, StringComparer.OrdinalIgnoreCase).ToList());

    public Task<Banca?> GetByIdAsync(Guid idBanca, CancellationToken cancellationToken = default)
        => Task.FromResult(Store.TryGetValue(idBanca, out var b) ? b : null);

    public Task<Banca?> GetByNomeAttivaAsync(string nome, CancellationToken cancellationToken = default)
        => Task.FromResult(Store.Values.FirstOrDefault(b =>
            b.IsAttivo && string.Equals(b.Nome, nome, StringComparison.OrdinalIgnoreCase)));

    public Task InsertAsync(Banca banca, CancellationToken cancellationToken = default)
    {
        Store[banca.IdBanca] = banca;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Banca banca, CancellationToken cancellationToken = default)
    {
        Store[banca.IdBanca] = banca;
        return Task.CompletedTask;
    }
}
