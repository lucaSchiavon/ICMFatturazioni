namespace ICMFatturazioni.Web.Models;

/// <summary>
/// Riga dell'elenco utenti per la UI di amministrazione: utente + nome del
/// ruolo (JOIN) e flag derivati. Read-model, non entità di dominio.
/// </summary>
public sealed class UtenteConRuolo
{
    public Guid IdUtente { get; init; }
    public required string Username { get; init; }
    public string? Email { get; init; }
    public Guid IdRuolo { get; init; }
    public required string RuoloNome { get; init; }
    public string? RuoloCodice { get; init; }
    public bool Attivo { get; init; }

    /// <summary>False = utente invitato senza password (da attivare, T4).</summary>
    public bool HaPassword { get; init; }

    public DateTime? UltimoLoginUtc { get; init; }
}
