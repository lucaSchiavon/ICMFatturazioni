namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Token monouso per l'attivazione account (invito) o il reset password.
/// POCO mappato su <c>fatt.UtenteToken</c> (migration 013, mirror di ICMVerbali).
/// </summary>
/// <remarks>
/// Il token in chiaro NON è mai persistito: in <see cref="TokenHash"/> si salva
/// solo il suo SHA-256 (32 byte). Lo stato è derivato dalle colonne nullable
/// (pattern CLAUDE.md): "Attivo" = <see cref="UsatoUtc"/> e
/// <see cref="RevocatoUtc"/> nulli e <see cref="ScadenzaUtc"/> futura.
/// </remarks>
public sealed class UtenteToken
{
    /// <summary>PK GUID (UUIDv7 app-side).</summary>
    public Guid Id { get; set; }

    /// <summary>Utente destinatario (FK → <c>fatt.Utenti</c>, ON DELETE CASCADE).</summary>
    public Guid UtenteId { get; set; }

    /// <summary>SHA-256 del token in chiaro (32 byte). Mai il token stesso.</summary>
    public byte[] TokenHash { get; set; } = [];

    /// <summary>Attivazione (invito) o Reset.</summary>
    public UtenteTokenTipo Tipo { get; set; }

    public DateTime ScadenzaUtc { get; set; }

    /// <summary>Istante di consumo (uso singolo); <c>null</c> se non ancora usato.</summary>
    public DateTime? UsatoUtc { get; set; }

    /// <summary>Istante di revoca (sostituito da un reinvio); <c>null</c> se attivo.</summary>
    public DateTime? RevocatoUtc { get; set; }

    public DateTime CreatedAt { get; set; }
}
