using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Doppio in-memory di <see cref="IAuditManager"/>: registra le chiamate per
/// poterle verificare nei test (es. che il <c>UtenteTokenManager</c> tracci
/// emissione e consumo dei magic-link).
/// </summary>
internal sealed class FakeAuditManager : IAuditManager
{
    public sealed record Voce(AuditOperazione Operazione, string EntityType, Guid EntityId, string? Descrizione, string? Dati);

    public List<Voce> Voci { get; } = new();

    public Task RegistraCreazioneAsync(string entityType, Guid entityId, string? descrizione, string? dati = null, CancellationToken cancellationToken = default)
        => Aggiungi(AuditOperazione.Creazione, entityType, entityId, descrizione, dati);

    public Task RegistraModificaAsync(string entityType, Guid entityId, string? descrizione, string? dati = null, CancellationToken cancellationToken = default)
        => Aggiungi(AuditOperazione.Modifica, entityType, entityId, descrizione, dati);

    public Task RegistraEliminazioneAsync(string entityType, Guid entityId, string? descrizione, string? dati = null, CancellationToken cancellationToken = default)
        => Aggiungi(AuditOperazione.Eliminazione, entityType, entityId, descrizione, dati);

    private Task Aggiungi(AuditOperazione op, string entityType, Guid entityId, string? descrizione, string? dati)
    {
        Voci.Add(new Voce(op, entityType, entityId, descrizione, dati));
        return Task.CompletedTask;
    }

    /// <summary>Mesi richiesti all'ultima purga (per i test che la verificano).</summary>
    public int? PurgaMesiRichiesti { get; private set; }

    // Non esercitati dai test che usano questo fake: stub minimi.
    public Task<AuditRisultato> CercaAsync(AuditFiltro filtro, CancellationToken cancellationToken = default)
        => Task.FromResult(new AuditRisultato(Array.Empty<Audit>(), 0));

    public Task<IReadOnlyList<Audit>> EsportaAsync(AuditFiltro filtro, int maxRighe, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Audit>>(Array.Empty<Audit>());

    public Task<IReadOnlyList<string>> GetEntityTypesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

    public Task<int> PurgaPrecedentiAsync(int mesi, CancellationToken cancellationToken = default)
    {
        PurgaMesiRichiesti = mesi;
        return Task.FromResult(0);
    }
}
