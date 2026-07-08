using ICMFatturazioni.Web.Services;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Storage in-memory per testare il filtro di esistenza fisica del PDF senza
/// toccare il filesystem. Un <c>ReportPath</c> è "presente" solo se è stato
/// aggiunto a <see cref="Presenti"/>; qualunque altro (incluso null) è assente.
/// </summary>
internal sealed class FakeVerbaleReportStorage : IVerbaleReportStorage
{
    public HashSet<string> Presenti { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool Esiste(string? reportPath)
        => reportPath is not null && Presenti.Contains(reportPath);

    public Task<byte[]?> LeggiAsync(string? reportPath, CancellationToken cancellationToken = default)
        => Task.FromResult(Esiste(reportPath) ? new byte[] { 1, 2, 3 } : null);
}
