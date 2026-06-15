using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Doppio in-memory di <see cref="IDatabaseSizeRepository"/>: restituisce una
/// dimensione configurabile, per pilotare la sentinella nei test.
/// </summary>
internal sealed class FakeDatabaseSizeRepository : IDatabaseSizeRepository
{
    public int DimensioneDatiMb { get; set; }

    public Task<int> GetDimensioneDatiMbAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(DimensioneDatiMb);
}
