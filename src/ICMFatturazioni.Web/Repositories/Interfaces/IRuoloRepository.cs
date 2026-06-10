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

    /// <summary>True se esiste già un ruolo con quel nome (escludendo un id).</summary>
    Task<bool> ExistsNomeAsync(string nome, Guid? escludiIdRuolo = null, CancellationToken cancellationToken = default);

    /// <summary>Numero di utenti a cui è assegnato il ruolo.</summary>
    Task<int> CountUtentiAsync(Guid idRuolo, CancellationToken cancellationToken = default);

    /// <summary>Inserisce un nuovo ruolo (custom; IdRuolo GUID v7 dal manager).</summary>
    Task InsertAsync(Ruolo ruolo, CancellationToken cancellationToken = default);

    /// <summary>Aggiorna nome, descrizione e stato attivo di un ruolo.</summary>
    Task UpdateAsync(Guid idRuolo, string nome, string? descrizione, bool isAttivo, CancellationToken cancellationToken = default);

    /// <summary>Elimina un ruolo e i suoi mapping di menu (per ruolo). Hard delete.</summary>
    Task DeleteAsync(Guid idRuolo, CancellationToken cancellationToken = default);
}
