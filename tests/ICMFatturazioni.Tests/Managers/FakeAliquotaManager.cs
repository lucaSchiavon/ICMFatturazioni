using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Fake di <see cref="IAliquotaManager"/>: espone solo la lettura delle aliquote di
/// sistema per il calcolo dell'avviso. Valori configurabili dai test (default 4/20).
/// </summary>
internal sealed class FakeAliquotaManager : IAliquotaManager
{
    public decimal Cnpaia   { get; set; } = 4m;
    public decimal Ritenuta { get; set; } = 20m;

    public Task<AliquoteFiscali> GetAliquoteAvvisoAsync(CancellationToken ct = default)
        => Task.FromResult(new AliquoteFiscali(Cnpaia, Ritenuta));

    public Task<IReadOnlyList<Aliquota>> ElencoAsync(CancellationToken ct = default)
        => throw new NotImplementedException();
    public Task<Aliquota?> GetByIdAsync(Guid idAliquota, CancellationToken ct = default)
        => throw new NotImplementedException();
    public Task<Guid> CreaAsync(Aliquota aliquota, CancellationToken ct = default)
        => throw new NotImplementedException();
    public Task AggiornaAsync(Aliquota aliquota, CancellationToken ct = default)
        => throw new NotImplementedException();
    public Task EliminaAsync(Guid idAliquota, CancellationToken ct = default)
        => throw new NotImplementedException();
}
