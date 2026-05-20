using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Repositories.Interfaces;

/// <summary>
/// Accesso dati alla tabella <c>dbo.Utenti</c>. Esposto al solo
/// <see cref="Managers.Interfaces.IUtenteManager"/> per rispettare la
/// regola "UI → Manager → Repository".
/// </summary>
public interface IUtenteRepository
{
    /// <summary>
    /// Restituisce l'utente con lo username indicato, o <c>null</c> se non
    /// esiste. Il confronto è case-insensitive (lo garantisce il collation
    /// di default della colonna).
    /// </summary>
    Task<Utente?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restituisce l'utente per chiave primaria, o <c>null</c>.
    /// </summary>
    Task<Utente?> GetByIdAsync(int idUtente, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserisce un nuovo utente. Ritorna l'<c>IdUtente</c> assegnato dall'identity.
    /// </summary>
    Task<int> InsertAsync(Utente utente, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggiorna il timestamp di ultimo login. Idempotente.
    /// </summary>
    Task UpdateUltimoLoginAsync(int idUtente, DateTime istanteUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cambia la preferenza di tema. Lascia inalterati gli altri campi.
    /// </summary>
    Task UpdateTemaPreferitoAsync(int idUtente, string temaPreferito, CancellationToken cancellationToken = default);
}
