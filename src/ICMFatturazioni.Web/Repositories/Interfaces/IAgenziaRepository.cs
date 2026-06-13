using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Repositories.Interfaces;

/// <summary>
/// Accesso dati all'anagrafica filiali <c>fatt.Agenzie</c>. Consumato dal solo
/// <see cref="Managers.Interfaces.IAgenziaManager"/>.
/// </summary>
public interface IAgenziaRepository
{
    /// <summary>
    /// Filiali attive di una banca, ordinate per nome. Alimenta la combo Agenzia
    /// (filtrata per la banca scelta).
    /// </summary>
    Task<IReadOnlyList<Agenzia>> GetByBancaAttiveAsync(Guid idBanca, CancellationToken cancellationToken = default);

    /// <summary>Recupera una filiale per id (anche disattivata), o <c>null</c>.</summary>
    Task<Agenzia?> GetByIdAsync(Guid idAgenzia, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recupera la filiale <b>attiva</b> di una banca con il nome indicato
    /// (case-insensitive), o <c>null</c>. Usato dalla logica "get-or-create".
    /// </summary>
    Task<Agenzia?> GetByNomeAttivaAsync(Guid idBanca, string nome, CancellationToken cancellationToken = default);

    /// <summary>Inserisce una nuova filiale (id GUID già valorizzato dal manager).</summary>
    Task InsertAsync(Agenzia agenzia, CancellationToken cancellationToken = default);

    /// <summary>Aggiorna una filiale esistente (nome/CAB).</summary>
    Task UpdateAsync(Agenzia agenzia, CancellationToken cancellationToken = default);
}
