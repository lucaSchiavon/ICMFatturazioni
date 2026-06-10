using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Repositories.Interfaces;

/// <summary>
/// Accesso dati alla tabella <c>fatt.Utenti</c>. Esposto al solo
/// <see cref="Managers.Interfaces.IUtenteManager"/> per rispettare la
/// regola "UI → Manager → Repository".
/// </summary>
public interface IUtenteRepository
{
    /// <summary>
    /// Utente con lo username indicato, o <c>null</c>. Confronto
    /// case-insensitive (collation di default della colonna).
    /// </summary>
    Task<Utente?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>Utente per chiave primaria (GUID), o <c>null</c>.</summary>
    Task<Utente?> GetByIdAsync(Guid idUtente, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserisce un nuovo utente. L'<c>IdUtente</c> (GUID v7) è già valorizzato
    /// dal manager prima della chiamata (no IDENTITY, no OUTPUT).
    /// </summary>
    Task InsertAsync(Utente utente, CancellationToken cancellationToken = default);

    /// <summary>Aggiorna il timestamp di ultimo login. Idempotente.</summary>
    Task UpdateUltimoLoginAsync(Guid idUtente, DateTime istanteUtc, CancellationToken cancellationToken = default);

    /// <summary>Cambia la preferenza di tema. Lascia inalterati gli altri campi.</summary>
    Task UpdateTemaPreferitoAsync(Guid idUtente, string temaPreferito, CancellationToken cancellationToken = default);
}
