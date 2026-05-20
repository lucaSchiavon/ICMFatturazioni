using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Managers.Interfaces;

/// <summary>
/// Logica applicativa sugli utenti: autenticazione, hashing password,
/// gestione preferenze. La UI deve sempre passare da qui — il
/// <see cref="Repositories.Interfaces.IUtenteRepository"/> non è iniettato
/// nei componenti Blazor.
/// </summary>
public interface IUtenteManager
{
    /// <summary>
    /// Verifica le credenziali ricevute dal form di login.
    /// Ritorna l'utente autenticato se username e password coincidono e
    /// l'utente è <see cref="Utente.Attivo"/>; altrimenti <c>null</c>.
    /// Mai distinguere il motivo del fallimento all'esterno (per non
    /// fornire enumeration ai tentativi di brute force).
    /// </summary>
    Task<Utente?> AutenticaAsync(string username, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea un nuovo utente con la password indicata in chiaro (hashata
    /// internamente prima dell'INSERT). Ritorna l'<c>IdUtente</c> assegnato.
    /// </summary>
    Task<int> CreaUtenteAsync(string username, string password, string? nomeCompleto, string? email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Garantisce l'esistenza dell'utente di sviluppo (Username
    /// <c>admin</c>, password <c>admin</c>). Idempotente: se l'utente
    /// esiste già non fa nulla. Va invocato solo in Environment Development
    /// e <b>deve essere rimosso</b> prima del rilascio in produzione.
    /// </summary>
    Task SeedUtenteSviluppoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggiorna la preferenza di tema dell'utente. Valori ammessi:
    /// <c>light</c>, <c>dark</c>, <c>auto</c>. Lancia
    /// <see cref="ArgumentOutOfRangeException"/> per valori fuori range.
    /// </summary>
    Task ImpostaTemaPreferitoAsync(int idUtente, string temaPreferito, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recupera utente per id. Esposto come canale "comodo" per la UI:
    /// permette al manager di centralizzare future invalidazioni cache.
    /// </summary>
    Task<Utente?> GetByIdAsync(int idUtente, CancellationToken cancellationToken = default);
}
