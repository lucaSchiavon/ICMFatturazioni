using ICMFatturazioni.Web.Authentication;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Models;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Implementazione di <see cref="IUtenteManager"/>. Concentra qui:
///   1) autenticazione (verifica password via <see cref="IPasswordHasherService"/>),
///   2) creazione utente (hashing + GUID v7 app-side),
///   3) validazione della preferenza tema.
/// L'hashing è delegato al servizio dedicato (PBKDF2 framework), non più
/// implementato a mano nel manager.
/// </summary>
internal sealed class UtenteManager : IUtenteManager
{
    // Valori tema replicati nel CHECK constraint della migration 006: tenerli
    // qui in un set immutabile evita un round-trip al DB per validare.
    private static readonly HashSet<string> TemiAmmessi = new(StringComparer.Ordinal)
    {
        "light", "dark", "auto",
    };

    private readonly IUtenteRepository _repository;
    private readonly IPasswordHasherService _passwordHasher;

    public UtenteManager(IUtenteRepository repository, IPasswordHasherService passwordHasher)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
    }

    // ---------------------------------------------------------------------
    // Autenticazione
    // ---------------------------------------------------------------------

    public async Task<Utente?> AutenticaAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            return null;
        }

        var utente = await _repository.GetByUsernameAsync(username, cancellationToken);

        // Non distinguiamo "inesistente" / "disattivato" / "invitato senza
        // password" / "password errata": l'attaccante non deve poter inferire
        // lo stato di un account.
        if (utente is null || !utente.Attivo || string.IsNullOrEmpty(utente.PasswordHash))
        {
            return null;
        }

        if (!_passwordHasher.VerifyHashedPassword(utente.PasswordHash, password))
        {
            return null;
        }

        // UltimoLoginUtc: se l'update fallisce non invalidiamo il login (già
        // verificato), ma propaghiamo l'eccezione perché venga loggata a monte.
        await _repository.UpdateUltimoLoginAsync(utente.IdUtente, DateTime.UtcNow, cancellationToken);
        return utente;
    }

    public Task<Utente?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
        => _repository.GetByUsernameAsync(username, cancellationToken);

    public Task<Utente?> GetByIdAsync(Guid idUtente, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(idUtente, cancellationToken);

    public Task<Utente?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => string.IsNullOrWhiteSpace(email)
            ? Task.FromResult<Utente?>(null)
            : _repository.GetByEmailAsync(email.Trim(), cancellationToken);

    // ---------------------------------------------------------------------
    // Creazione utente
    // ---------------------------------------------------------------------

    public async Task<Guid> CreaAsync(
        string username,
        string? password,
        string? email,
        Guid idRuolo,
        string? nomeCompleto = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Lo username è obbligatorio.", nameof(username));
        }
        if (idRuolo == Guid.Empty)
        {
            throw new ArgumentException("Il ruolo è obbligatorio.", nameof(idRuolo));
        }

        // Username univoco: pre-check user-friendly (la unique sul DB è la
        // difesa finale sotto race condition).
        if (await _repository.ExistsUsernameAsync(username, null, cancellationToken))
        {
            throw new UtenteDuplicatoException(username);
        }

        // Password opzionale: null/vuota → utente invitato (PasswordHash null),
        // che imposterà la password via link di attivazione (T4). Se valorizzata,
        // deve rispettare la policy condivisa prima dell'hashing.
        string? passwordHash = null;
        if (!string.IsNullOrEmpty(password))
        {
            var errore = PasswordPolicy.Valida(password);
            if (errore is not null)
            {
                throw new ArgumentException(errore, nameof(password));
            }
            passwordHash = _passwordHasher.HashPassword(password);
        }

        var nuovo = new Utente
        {
            IdUtente = Guid.CreateVersion7(),   // UUIDv7 time-ordered, app-side (ADR D22)
            Username = username,
            Email = email,
            PasswordHash = passwordHash,
            IdRuolo = idRuolo,
            NomeCompleto = nomeCompleto,
            Attivo = true,
            TemaPreferito = "light",
        };

        await _repository.InsertAsync(nuovo, cancellationToken);
        return nuovo.IdUtente;
    }

    // ---------------------------------------------------------------------
    // Preferenza tema
    // ---------------------------------------------------------------------

    public Task ImpostaTemaPreferitoAsync(Guid idUtente, string temaPreferito, CancellationToken cancellationToken = default)
    {
        if (!TemiAmmessi.Contains(temaPreferito))
        {
            throw new ArgumentOutOfRangeException(
                nameof(temaPreferito),
                temaPreferito,
                $"Tema non valido. Valori ammessi: {string.Join(", ", TemiAmmessi)}.");
        }
        return _repository.UpdateTemaPreferitoAsync(idUtente, temaPreferito, cancellationToken);
    }

    // ---------------------------------------------------------------------
    // Amministrazione utenti (T3)
    // ---------------------------------------------------------------------

    public Task<IReadOnlyList<UtenteConRuolo>> ElencoAsync(CancellationToken cancellationToken = default)
        => _repository.GetAllConRuoloAsync(cancellationToken);

    public async Task AggiornaAsync(Guid idUtente, string username, string? email, Guid idRuolo, bool attivo, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Lo username è obbligatorio.", nameof(username));
        }
        if (idRuolo == Guid.Empty)
        {
            throw new ArgumentException("Il ruolo è obbligatorio.", nameof(idRuolo));
        }
        if (await _repository.ExistsUsernameAsync(username, idUtente, cancellationToken))
        {
            throw new UtenteDuplicatoException(username);
        }
        await _repository.UpdateProfiloAsync(idUtente, username, email, idRuolo, attivo, cancellationToken);
    }

    public async Task ImpostaPasswordAsync(Guid idUtente, string nuovaPassword, CancellationToken cancellationToken = default)
    {
        var errore = PasswordPolicy.Valida(nuovaPassword);
        if (errore is not null)
        {
            throw new ArgumentException(errore, nameof(nuovaPassword));
        }
        var hash = _passwordHasher.HashPassword(nuovaPassword);
        await _repository.UpdatePasswordHashAsync(idUtente, hash, cancellationToken);
    }
}
