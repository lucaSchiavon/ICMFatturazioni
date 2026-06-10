using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Managers.Interfaces;

/// <summary>
/// Logica applicativa sugli utenti: autenticazione, creazione, preferenze.
/// La UI deve sempre passare da qui — il
/// <see cref="Repositories.Interfaces.IUtenteRepository"/> non è iniettato
/// nei componenti Blazor.
/// </summary>
public interface IUtenteManager
{
    /// <summary>
    /// Verifica le credenziali del form di login. Ritorna l'utente se
    /// username/password coincidono, l'utente è <see cref="Utente.Attivo"/> e
    /// ha una password impostata; altrimenti <c>null</c>. Non distingue mai il
    /// motivo del fallimento (anti-enumeration).
    /// </summary>
    Task<Utente?> AutenticaAsync(string username, string password, CancellationToken cancellationToken = default);

    /// <summary>Utente per username, o <c>null</c>.</summary>
    Task<Utente?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>Utente per id (GUID), o <c>null</c>.</summary>
    Task<Utente?> GetByIdAsync(Guid idUtente, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea un nuovo utente assegnandogli il ruolo indicato. La password è
    /// opzionale: se <c>null</c> l'utente nasce "invitato" (senza password,
    /// da attivare via link — T4); se valorizzata viene hashata prima
    /// dell'INSERT. Ritorna l'<c>IdUtente</c> (GUID v7) generato.
    /// </summary>
    Task<Guid> CreaAsync(
        string username,
        string? password,
        string? email,
        Guid idRuolo,
        string? nomeCompleto = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggiorna la preferenza di tema (<c>light</c>/<c>dark</c>/<c>auto</c>).
    /// Lancia <see cref="ArgumentOutOfRangeException"/> per valori fuori range.
    /// </summary>
    Task ImpostaTemaPreferitoAsync(Guid idUtente, string temaPreferito, CancellationToken cancellationToken = default);
}
