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

    /// <summary>Tipi di entità distinti presenti in tabella (per popolare il filtro UI).</summary>
    Task<IReadOnlyList<string>> GetEntityTypesAsync(CancellationToken cancellationToken = default);
}
