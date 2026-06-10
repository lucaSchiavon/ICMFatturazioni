using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Models;

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

    /// <summary>Elenco utenti con il nome del ruolo, per la UI di amministrazione.</summary>
    Task<IReadOnlyList<UtenteConRuolo>> ElencoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggiorna profilo (username, email, ruolo, attivo) di un utente esistente.
    /// Lancia <see cref="UtenteDuplicatoException"/> se lo username è già usato
    /// da un altro utente. Non tocca la password.
    /// </summary>
    Task AggiornaAsync(Guid idUtente, string username, string? email, Guid idRuolo, bool attivo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Imposta (o reimposta) la password di un utente: la valida e la hasha.
    /// Usato dall'admin in T3; il reset via email/token è T4.
    /// </summary>
    Task ImpostaPasswordAsync(Guid idUtente, string nuovaPassword, CancellationToken cancellationToken = default);
}
