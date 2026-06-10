using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Models;

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
    /// Utente con quell'email (confronto case-insensitive), o <c>null</c>.
    /// Usato dal flusso "password dimenticata" (T4). L'email è univoca quando
    /// presente (indice filtrato).
    /// </summary>
    Task<Utente?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserisce un nuovo utente. L'<c>IdUtente</c> (GUID v7) è già valorizzato
    /// dal manager prima della chiamata (no IDENTITY, no OUTPUT).
    /// </summary>
    Task InsertAsync(Utente utente, CancellationToken cancellationToken = default);

    /// <summary>Aggiorna il timestamp di ultimo login. Idempotente.</summary>
    Task UpdateUltimoLoginAsync(Guid idUtente, DateTime istanteUtc, CancellationToken cancellationToken = default);

    /// <summary>Cambia la preferenza di tema. Lascia inalterati gli altri campi.</summary>
    Task UpdateTemaPreferitoAsync(Guid idUtente, string temaPreferito, CancellationToken cancellationToken = default);

    /// <summary>Elenco utenti con il nome del ruolo (JOIN), per la UI admin.</summary>
    Task<IReadOnlyList<UtenteConRuolo>> GetAllConRuoloAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// True se esiste già un utente con quello username (confronto
    /// case-insensitive), escludendo eventualmente un id (per la modifica).
    /// </summary>
    Task<bool> ExistsUsernameAsync(string username, Guid? escludiIdUtente = null, CancellationToken cancellationToken = default);

    /// <summary>Aggiorna profilo (username, email, ruolo, attivo). Non tocca la password.</summary>
    Task UpdateProfiloAsync(Guid idUtente, string username, string? email, Guid idRuolo, bool attivo, CancellationToken cancellationToken = default);

    /// <summary>Imposta il nuovo hash password (o <c>null</c> per "da attivare").</summary>
    Task UpdatePasswordHashAsync(Guid idUtente, string? passwordHash, CancellationToken cancellationToken = default);
}
