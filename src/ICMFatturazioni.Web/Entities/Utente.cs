namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Utente applicativo autenticato via cookie. POCO mappato su
/// <c>fatt.Utenti</c>: nessuna dipendenza da Dapper, EF o ASP.NET.
/// </summary>
/// <remarks>
/// <para>
/// La password è custodita in <see cref="PasswordHash"/> come singola stringa
/// in formato PBKDF2 v3 di <c>PasswordHasher&lt;T&gt;</c> (salt incluso): non
/// esiste più una colonna salt separata. L'hashing/verifica vive in
/// <c>IPasswordHasherService</c>, non qui.
/// </para>
/// <para>
/// <see cref="PasswordHash"/> è <b>nullable</b>: un utente creato dall'admin
/// nasce senza password (<c>null</c> = "invitato, da attivare") e la imposta
/// tramite link di attivazione (Tappa T4). Fino ad allora il login fallisce.
/// </para>
/// </remarks>
public sealed class Utente
{
    /// <summary>PK GUID (UUIDv7 generato app-side dal manager, ADR D22).</summary>
    public Guid IdUtente { get; set; }

    /// <summary>Identificativo logico univoco usato per il login.</summary>
    public required string Username { get; init; }

    /// <summary>Email opzionale (univoca se presente): usata per il reset password.</summary>
    public string? Email { get; init; }

    /// <summary>
    /// Hash PBKDF2 della password (formato v3, salt incluso). <c>null</c> =
    /// utente invitato non ancora attivato.
    /// </summary>
    public string? PasswordHash { get; init; }

    /// <summary>Ruolo assegnato (FK → <c>fatt.Ruoli</c>). Ne eredita i permessi.</summary>
    public Guid IdRuolo { get; init; }

    /// <summary>Nome esteso da mostrare in barra utente, header, log.</summary>
    public string? NomeCompleto { get; init; }

    /// <summary>Falso quando l'utente è disabilitato: il login fallisce.</summary>
    public bool Attivo { get; init; } = true;

    /// <summary>
    /// Preferenza di tema: <c>light</c>, <c>dark</c> o <c>auto</c>.
    /// </summary>
    public string TemaPreferito { get; init; } = "light";

    /// <summary>Timestamp dell'ultimo login riuscito (UTC), null se mai loggato.</summary>
    public DateTime? UltimoLoginUtc { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
