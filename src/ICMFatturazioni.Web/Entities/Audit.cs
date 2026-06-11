namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Tipo di operazione tracciata nell'audit. Append-only: nuovi valori si
/// aggiungono in coda mantenendo i numerici esistenti (persistiti come TINYINT).
/// </summary>
public enum AuditOperazione : byte
{
    Creazione = 0,
    Modifica = 1,
    Eliminazione = 2,
}

/// <summary>
/// Riga della tabella <c>fatt.Audit</c> ("chi-ha-fatto-cosa"). POCO senza
/// dipendenze. Immutabile per convenzione: solo INSERT.
/// </summary>
/// <remarks>
/// <see cref="UtenteNome"/> è uno <b>snapshot</b> del nome al momento
/// dell'azione: resta leggibile anche se l'utente viene poi rinominato o
/// eliminato (la tabella non ha FK proprio per consentirlo).
/// </remarks>
public sealed class Audit
{
    public Guid Id { get; set; }
    public DateTime TimestampUtc { get; set; }

    // Chi
    public Guid? UtenteId { get; set; }
    public string? UtenteNome { get; set; }

    public AuditOperazione Operazione { get; set; }

    // Cosa
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }

    /// <summary>Etichetta breve, leggibile (mostrata in griglia).</summary>
    public string? Descrizione { get; set; }

    /// <summary>
    /// Dettaglio strutturato in JSON: snapshot dei campi (insert/delete) o diff
    /// prima→dopo (update). Mai contiene segreti (esclusi a monte). Nullable.
    /// </summary>
    public string? Dati { get; set; }
}
