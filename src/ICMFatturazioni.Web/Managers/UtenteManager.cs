using System.Security.Cryptography;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Repositories.Interfaces;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Implementazione di <see cref="IUtenteManager"/>. Concentra qui le tre
/// responsabilità "delicate" sull'utente:
///   1) hashing/verify della password (PBKDF2-HMAC-SHA256),
///   2) validazione delle preferenze (tema),
///   3) seed dello utente dev (idempotente).
/// </summary>
internal sealed class UtenteManager : IUtenteManager
{
    // -----------------------------------------------------------------
    // Parametri PBKDF2
    // -----------------------------------------------------------------
    // 100k iterazioni HMAC-SHA256 è un compromesso ragionevole per il
    // 2026 (linee guida OWASP "Password Storage Cheat Sheet"): ~60ms su
    // hardware moderno, sufficiente a frenare un brute force offline
    // pur non bloccando l'esperienza di login. Salt 16 byte e key 32
    // byte coincidono con i tipi VARBINARY definiti in migration 002.
    private const KeyDerivationPrf Prf = KeyDerivationPrf.HMACSHA256;
    private const int IterationCount = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    // Tema: i 3 valori ammessi sono replicati anche nel CHECK constraint
    // della migration 002. Tenerli qui in un set immutabile evita un
    // round-trip al DB per validare.
    private static readonly HashSet<string> TemiAmmessi = new(StringComparer.Ordinal)
    {
        "light", "dark", "auto",
    };

    private readonly IUtenteRepository _repository;

    public UtenteManager(IUtenteRepository repository)
    {
        _repository = repository;
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
        if (utente is null || !utente.Attivo)
        {
            // Non distinguiamo "utente inesistente" da "utente disattivato":
            // l'attaccante non deve poter inferire la presenza di un account.
            return null;
        }

        if (!VerificaPassword(password, utente.PasswordSalt, utente.PasswordHash))
        {
            return null;
        }

        // Aggiornamento UltimoLoginUtc out-of-band: se fallisce non
        // invalidiamo il login (già verificato), ma propaghiamo
        // l'eccezione perché il middleware errori la logghi.
        await _repository.UpdateUltimoLoginAsync(utente.IdUtente, DateTime.UtcNow, cancellationToken);
        return utente;
    }

    // ---------------------------------------------------------------------
    // Creazione utente
    // ---------------------------------------------------------------------

    public async Task<int> CreaUtenteAsync(string username, string password, string? nomeCompleto, string? email, CancellationToken cancellationToken = default)
    {
        // Le validazioni di forma stanno qui (single responsibility del
        // manager). Il repository assume gli input già validati.
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Lo username è obbligatorio.", nameof(username));
        }
        if (string.IsNullOrEmpty(password) || password.Length < 8)
        {
            throw new ArgumentException("La password deve avere almeno 8 caratteri.", nameof(password));
        }

        var (hash, salt) = HashPassword(password);

        var nuovo = new Utente
        {
            Username = username,
            PasswordHash = hash,
            PasswordSalt = salt,
            NomeCompleto = nomeCompleto,
            Email = email,
            Attivo = true,
            TemaPreferito = "light",
        };

        return await _repository.InsertAsync(nuovo, cancellationToken);
    }

    // ---------------------------------------------------------------------
    // Seed dev-only
    // ---------------------------------------------------------------------

    public async Task SeedUtenteSviluppoAsync(CancellationToken cancellationToken = default)
    {
        // Idempotente: se "admin" esiste già non lo ricreiamo.
        var esistente = await _repository.GetByUsernameAsync("admin", cancellationToken);
        if (esistente is not null)
        {
            return;
        }
        await CreaUtenteAsync(
            username: "admin",
            password: "admin1234", // dev-only, da rimuovere prima del rilascio
            nomeCompleto: "Amministratore (dev)",
            email: null,
            cancellationToken: cancellationToken);
    }

    // ---------------------------------------------------------------------
    // Preferenza tema
    // ---------------------------------------------------------------------

    public Task ImpostaTemaPreferitoAsync(int idUtente, string temaPreferito, CancellationToken cancellationToken = default)
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

    public Task<Utente?> GetByIdAsync(int idUtente, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(idUtente, cancellationToken);

    // ---------------------------------------------------------------------
    // Helper crittografici (privati: l'esterno non li vede)
    // ---------------------------------------------------------------------

    private static (byte[] Hash, byte[] Salt) HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = KeyDerivation.Pbkdf2(
            password: password,
            salt: salt,
            prf: Prf,
            iterationCount: IterationCount,
            numBytesRequested: HashSize);
        return (hash, salt);
    }

    private static bool VerificaPassword(string password, byte[] salt, byte[] expectedHash)
    {
        var actualHash = KeyDerivation.Pbkdf2(
            password: password,
            salt: salt,
            prf: Prf,
            iterationCount: IterationCount,
            numBytesRequested: HashSize);
        // Confronto a tempo costante per non perdere informazione tramite
        // timing attack (l'attaccante non deve poter desumere quanti byte
        // hanno coinciso da quanto tempo prende il confronto).
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
