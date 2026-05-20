namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Utente applicativo autenticato via cookie. Entità POCO mappata 1:1
/// sulla tabella <c>dbo.Utenti</c>: nessuna dipendenza da Dapper, EF o
/// ASP.NET. La logica di hashing/verify password vive nel manager.
/// </summary>
/// <remarks>
/// I campi <see cref="PasswordHash"/> e <see cref="PasswordSalt"/> sono
/// volutamente esposti come <c>byte[]</c>: chi consuma l'entità (manager
/// di autenticazione) deve poter verificare la password, ma <b>nessun
/// consumatore al di fuori del manager</b> deve mai accederci. Tenerli
/// come array piuttosto che stringa Base64 evita conversioni multiple
/// nel percorso critico e rende esplicito che sono "blob crittografici".
/// </remarks>
public sealed class Utente
{
    /// <summary>Chiave primaria identity.</summary>
    public int IdUtente { get; init; }

    /// <summary>Identificativo logico univoco usato per il login.</summary>
    public required string Username { get; init; }

    /// <summary>Derived key PBKDF2 (32 byte) prodotta dal manager.</summary>
    public required byte[] PasswordHash { get; init; }

    /// <summary>Salt casuale per utente (16 byte).</summary>
    public required byte[] PasswordSalt { get; init; }

    /// <summary>Nome esteso da mostrare in barra utente, header, log.</summary>
    public string? NomeCompleto { get; init; }

    /// <summary>Email opzionale per reset password futuro.</summary>
    public string? Email { get; init; }

    /// <summary>Falso quando l'utente è stato disabilitato: il login fallisce.</summary>
    public bool Attivo { get; init; } = true;

    /// <summary>
    /// Preferenza di tema dell'utente. Valori ammessi: <c>light</c>,
    /// <c>dark</c>, <c>auto</c> (l'app segue la preferenza di sistema).
    /// </summary>
    public string TemaPreferito { get; init; } = "light";

    /// <summary>Audit di creazione/aggiornamento riga (UTC).</summary>
    public DateTime DataRecord { get; init; }

    /// <summary>Timestamp dell'ultimo login riuscito (UTC), null se mai loggato.</summary>
    public DateTime? UltimoLoginUtc { get; init; }
}
