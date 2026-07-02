using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Web.Managers.Interfaces;

/// <summary>
/// Registra le operazioni CRUD sui dati master in <c>fatt.Audit</c>
/// ("chi-ha-fatto-cosa"). L'utente corrente è risolto internamente via
/// <see cref="ICurrentUserAccessor"/>: il chiamante passa solo il "cosa".
/// </summary>
/// <remarks>
/// Best-effort: un fallimento della scrittura di audit NON deve far fallire
/// l'operazione di business già completata; viene loggato e ingoiato.
/// Da invocare <b>dopo</b> che la modifica al DB è andata a buon fine.
/// </remarks>
public interface IAuditManager
{
    Task RegistraCreazioneAsync(string entityType, Guid entityId, string? descrizione, string? dati = null, CancellationToken cancellationToken = default);
    Task RegistraModificaAsync(string entityType, Guid entityId, string? descrizione, string? dati = null, CancellationToken cancellationToken = default);
    Task RegistraEliminazioneAsync(string entityType, Guid entityId, string? descrizione, string? dati = null, CancellationToken cancellationToken = default);

    /// <summary>Ricerca paginata dell'audit (pagina di amministrazione <c>/admin/audit</c>).</summary>
    Task<AuditRisultato> CercaAsync(AuditFiltro filtro, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tutte le righe che soddisfano il filtro (export CSV), fino a
    /// <paramref name="maxRighe"/>. Ignora la paginazione della griglia.
    /// </summary>
    Task<IReadOnlyList<Audit>> EsportaAsync(AuditFiltro filtro, int maxRighe, CancellationToken cancellationToken = default);

    /// <summary>Tipi di entità distinti presenti, per popolare il filtro UI.</summary>
    Task<IReadOnlyList<string>> GetEntityTypesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retention temporale: elimina le righe di audit più vecchie di
    /// <paramref name="mesi"/> mesi. Ritorna le righe eliminate. La purga di per
    /// sé NON viene a sua volta auditata (è manutenzione, non un'operazione di
    /// dominio dell'utente). Usata sia dal job automatico (AuditRetentionService)
    /// sia dal pulsante manuale in <c>/admin/audit</c>; idempotente.
    /// </summary>
    Task<int> PurgaPrecedentiAsync(int mesi, CancellationToken cancellationToken = default);
}
