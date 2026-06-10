using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Repositories.Interfaces;

/// <summary>
/// Accesso dati a <c>fatt.Ruoli</c>. In T1 espone le sole letture necessarie
/// ad autenticazione e seed; il CRUD completo (creazione/modifica ruoli
/// custom) arriverà con la UI di amministrazione (T3).
/// </summary>
public interface IRuoloRepository
{
    /// <summary>Tutti i ruoli, ordinati per nome (sistema prima).</summary>
    Task<IReadOnlyList<Ruolo>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Ruolo per id, o <c>null</c>.</summary>
    Task<Ruolo?> GetByIdAsync(Guid idRuolo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ruolo per codice di sistema (<c>SUPERADMIN</c>/<c>ADMIN</c>), o
    /// <c>null</c>. Usato dal seeder per risolvere l'IdRuolo dei ruoli fissi.
    /// </summary>
    Task<Ruolo?> GetByCodiceAsync(string codice, CancellationToken cancellationToken = default);
}
