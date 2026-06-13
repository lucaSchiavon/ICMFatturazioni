using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Repositories.Interfaces;

/// <summary>
/// Accesso dati all'anagrafica istituti <c>fatt.Banche</c>. Consumato dal solo
/// <see cref="Managers.Interfaces.IBancaManager"/>.
/// </summary>
public interface IBancaRepository
{
    /// <summary>Istituti attivi, ordinati per nome. Alimenta la combo Banca.</summary>
    Task<IReadOnlyList<Banca>> GetAttiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Recupera un istituto per id (anche disattivato), o <c>null</c>.</summary>
    Task<Banca?> GetByIdAsync(Guid idBanca, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recupera l'istituto <b>attivo</b> con il nome indicato (case-insensitive),
    /// o <c>null</c>. Usato dalla logica "get-or-create".
    /// </summary>
    Task<Banca?> GetByNomeAttivaAsync(string nome, CancellationToken cancellationToken = default);

    /// <summary>Inserisce un nuovo istituto (id GUID già valorizzato dal manager).</summary>
    Task InsertAsync(Banca banca, CancellationToken cancellationToken = default);

    /// <summary>Aggiorna un istituto esistente (nome/ABI).</summary>
    Task UpdateAsync(Banca banca, CancellationToken cancellationToken = default);
}
