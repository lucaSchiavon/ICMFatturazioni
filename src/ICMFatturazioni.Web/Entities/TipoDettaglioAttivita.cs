namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Tipo di dettaglio per una riga di attività (es. DISCIPLINARE, EXTRA DISCIPLINARE).
/// Catalogo amministrativo con soft-delete. POCO senza dipendenze da Dapper/EF/ASP.NET.
/// </summary>
public sealed class TipoDettaglioAttivita
{
    /// <summary>Chiave primaria GUID (UUIDv7 app-side, ADR D22).</summary>
    public Guid IdTipoDettaglioAttivita { get; set; }

    /// <summary>Descrizione del tipo dettaglio. Obbligatoria e univoca tra gli attivi.
    /// Corrisponde alla colonna SQL <c>TipoDettaglioAttivita</c>.</summary>
    public required string Descrizione { get; init; }

    /// <summary>Soft-delete (ADR D22). Default <c>true</c>.</summary>
    public bool IsAttivo { get; init; } = true;
}
