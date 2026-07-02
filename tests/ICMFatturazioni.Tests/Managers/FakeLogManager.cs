using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Doppio in-memory di <see cref="ILogManager"/>: registra gli errori loggati,
/// per verificare ad es. che l'<c>AuditManager</c> tracci un fallimento di
/// scrittura senza rilanciarlo.
/// </summary>
internal sealed class FakeLogManager : ILogManager
{
    public sealed record Voce(Exception Eccezione, string Spiegazione, string Sorgente);

    public List<Voce> Errori { get; } = new();

    public Task LogErroreAsync(
        Exception eccezione, string spiegazione, string sorgente,
        Guid? utenteId = null, Guid? entityId = null, string? entityType = null,
        CancellationToken cancellationToken = default)
    {
        Errori.Add(new Voce(eccezione, spiegazione, sorgente));
        return Task.CompletedTask;
    }

    // Non esercitati dai test che usano questo fake: stub minimi.
    public Task<LogRisultato> CercaAsync(LogFiltro filtro, CancellationToken cancellationToken = default)
        => Task.FromResult(new LogRisultato(Array.Empty<Web.Entities.Log>(), 0));

    public Task<IReadOnlyList<Web.Entities.Log>> EsportaAsync(LogFiltro filtro, int maxRighe, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Web.Entities.Log>>(Array.Empty<Web.Entities.Log>());

    public Task<int> PurgaPrecedentiAsync(int giorni, CancellationToken cancellationToken = default)
        => Task.FromResult(0);
}
