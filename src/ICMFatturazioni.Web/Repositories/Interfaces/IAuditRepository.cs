using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Web.Repositories.Interfaces;

/// <summary>
/// Accesso dati a <c>fatt.Audit</c>: scrittura e ricerca paginata per la pagina
/// <c>/admin/audit</c>.
/// </summary>
public interface IAuditRepository
{
    Task InsertAsync(Audit entry, CancellationToken cancellationToken = default);

    /// <summary>Ricerca paginata, ordinata per timestamp decrescente.</summary>
    Task<AuditRisultato> CercaAsync(AuditFiltro filtro, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tutte le righe che soddisfano il filtro (per l'export CSV), fino a
    /// <paramref name="maxRighe"/>, ordinate per timestamp decrescente. Non
    /// applica la paginazione della griglia.
    /// </summary>
    Task<IReadOnlyList<Audit>> EsportaAsync(AuditFiltro filtro, int maxRighe, CancellationToken cancellationToken = default);

    /// <summary>Tipi di entità distinti presenti in tabella (per popolare il filtro UI).</summary>
    Task<IReadOnlyList<string>> GetEntityTypesAsync(CancellationToken cancellationToken = default);

    /// <summary>Elimina le righe con <c>TimestampUtc &lt; soglia</c>. Ritorna le righe eliminate.</summary>
    Task<int> PurgaPrecedentiAsync(DateTime sogliaUtc, CancellationToken cancellationToken = default);
}
