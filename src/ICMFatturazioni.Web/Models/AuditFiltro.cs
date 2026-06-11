using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Models;

/// <summary>
/// Criteri di ricerca per la consultazione di <c>fatt.Audit</c> (pagina
/// <c>/admin/audit</c>). Tutti i filtri sono opzionali e si combinano in AND.
/// </summary>
public sealed record AuditFiltro
{
    public DateTime? DaUtc { get; init; }
    public DateTime? AUtc { get; init; }

    /// <summary>Filtro esatto sull'operazione (Creazione/Modifica/Eliminazione).</summary>
    public AuditOperazione? Operazione { get; init; }

    /// <summary>Match esatto sul tipo di entità (es. "Anagrafica", "UtenteToken").</summary>
    public string? EntityType { get; init; }

    /// <summary>Match parziale (LIKE) su nome utente o descrizione.</summary>
    public string? Testo { get; init; }

    public int Pagina { get; init; } = 1;
    public int Dimensione { get; init; } = 25;
}

/// <summary>Pagina di risultati della ricerca audit (righe + totale complessivo).</summary>
public sealed record AuditRisultato(IReadOnlyList<Audit> Righe, int TotaleRighe);
