using ICMFatturazioni.Web.Authentication;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Models;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Implementazione di <see cref="IAuditManager"/> (mirror di ICMVerbali).
/// Cattura lo snapshot di id+nome dell'utente corrente al momento dell'azione e
/// tronca la descrizione alla capacità della colonna.
/// </summary>
internal sealed class AuditManager : IAuditManager
{
    private const int MaxDescrizione = 512;

    private readonly IAuditRepository _repository;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly ILogManager _logManager;
    private readonly TimeProvider _clock;

    public AuditManager(
        IAuditRepository repository,
        ICurrentUserAccessor currentUser,
        ILogManager logManager,
        TimeProvider clock)
    {
        _repository = repository;
        _currentUser = currentUser;
        _logManager = logManager;
        _clock = clock;
    }

    public Task RegistraCreazioneAsync(string entityType, Guid entityId, string? descrizione, string? dati = null, CancellationToken cancellationToken = default)
        => RegistraAsync(AuditOperazione.Creazione, entityType, entityId, descrizione, dati, cancellationToken);

    public Task RegistraModificaAsync(string entityType, Guid entityId, string? descrizione, string? dati = null, CancellationToken cancellationToken = default)
        => RegistraAsync(AuditOperazione.Modifica, entityType, entityId, descrizione, dati, cancellationToken);

    public Task RegistraEliminazioneAsync(string entityType, Guid entityId, string? descrizione, string? dati = null, CancellationToken cancellationToken = default)
        => RegistraAsync(AuditOperazione.Eliminazione, entityType, entityId, descrizione, dati, cancellationToken);

    private async Task RegistraAsync(
        AuditOperazione operazione, string entityType, Guid entityId, string? descrizione, string? dati, CancellationToken cancellationToken)
    {
        var (utenteId, utenteNome) = await _currentUser.GetAsync(cancellationToken);
        var entry = new Audit
        {
            Id = Guid.CreateVersion7(),
            TimestampUtc = _clock.GetUtcNow().UtcDateTime,
            UtenteId = utenteId,
            UtenteNome = utenteNome,
            Operazione = operazione,
            EntityType = entityType,
            EntityId = entityId,
            Descrizione = Tronca(descrizione, MaxDescrizione),
            Dati = dati,
        };

        try
        {
            await _repository.InsertAsync(entry, cancellationToken);
        }
        catch (Exception ex)
        {
            // L'operazione di business è già avvenuta: un fallimento dell'audit
            // non deve propagarsi al chiamante (apparirebbe come un errore
            // dell'operazione). Lo registriamo e proseguiamo (Regola 6).
            await _logManager.LogErroreAsync(ex,
                $"Registrazione audit '{operazione}' su {entityType} fallita. L'operazione " +
                "applicativa è comunque andata a buon fine; manca solo la traccia di audit.",
                "AuditManager.RegistraAsync",
                utenteId: utenteId,
                entityId: entityId,
                entityType: entityType,
                cancellationToken: cancellationToken);
        }
    }

    public Task<AuditRisultato> CercaAsync(AuditFiltro filtro, CancellationToken cancellationToken = default)
        => _repository.CercaAsync(filtro, cancellationToken);

    public Task<IReadOnlyList<Audit>> EsportaAsync(AuditFiltro filtro, int maxRighe, CancellationToken cancellationToken = default)
        => _repository.EsportaAsync(filtro, maxRighe, cancellationToken);

    public Task<IReadOnlyList<string>> GetEntityTypesAsync(CancellationToken cancellationToken = default)
        => _repository.GetEntityTypesAsync(cancellationToken);

    public Task<int> PurgaPrecedentiAsync(int mesi, CancellationToken cancellationToken = default)
    {
        // Soglia calcolata col TimeProvider (testabile). |mesi| per tollerare un
        // input negativo accidentale dalla UI senza purgare il futuro.
        var soglia = _clock.GetUtcNow().UtcDateTime.AddMonths(-Math.Abs(mesi));
        return _repository.PurgaPrecedentiAsync(soglia, cancellationToken);
    }

    private static string? Tronca(string? s, int max)
        => s is not null && s.Length > max ? s[..max] : s;
}
