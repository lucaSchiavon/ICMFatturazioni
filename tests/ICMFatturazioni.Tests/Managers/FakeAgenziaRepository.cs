using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>In-memory di <see cref="IAgenziaRepository"/> per i test (no DB).</summary>
internal sealed class FakeAgenziaRepository : IAgenziaRepository
{
    public Dictionary<Guid, Agenzia> Store { get; } = new();

    public Task<IReadOnlyList<Agenzia>> GetByBancaAttiveAsync(Guid idBanca, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Agenzia>>(
            Store.Values.Where(a => a.IsAttivo && a.IdBanca == idBanca)
                .OrderBy(a => a.Nome, StringComparer.OrdinalIgnoreCase).ToList());

    public Task<Agenzia?> GetByIdAsync(Guid idAgenzia, CancellationToken cancellationToken = default)
        => Task.FromResult(Store.TryGetValue(idAgenzia, out var a) ? a : null);

    public Task<Agenzia?> GetByNomeAttivaAsync(Guid idBanca, string nome, CancellationToken cancellationToken = default)
        => Task.FromResult(Store.Values.FirstOrDefault(a =>
            a.IsAttivo && a.IdBanca == idBanca && string.Equals(a.Nome, nome, StringComparison.OrdinalIgnoreCase)));

    public Task InsertAsync(Agenzia agenzia, CancellationToken cancellationToken = default)
    {
        Store[agenzia.IdAgenzia] = agenzia;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Agenzia agenzia, CancellationToken cancellationToken = default)
    {
        Store[agenzia.IdAgenzia] = agenzia;
        return Task.CompletedTask;
    }
}
