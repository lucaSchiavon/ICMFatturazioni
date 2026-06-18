namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Tipo di attività dello studio (cap. 9, dispensa). Catalogo amministrativo
/// con soft-delete (IsAttivo). POCO senza dipendenze da Dapper/EF/ASP.NET.
/// </summary>
public sealed class TipoAttivita
{
    /// <summary>Chiave primaria GUID (UUIDv7 app-side, ADR D22).</summary>
    public Guid IdTipoAttivita { get; set; }

    /// <summary>Descrizione del tipo (es. "CONSULENZE"). Obbligatoria e univoca tra gli attivi.
    /// Corrisponde alla colonna SQL <c>TipoAttivita</c>.</summary>
    public required string Descrizione { get; init; }

    /// <summary>Modalità di gestione: governa il comportamento nell'inserimento attività.</summary>
    public required GestisciCome GestisciCome { get; init; }

    /// <summary>Flag Studi di Settore (funzionalità rinviata; presente per compatibilità legacy).</summary>
    public bool StudiSettore { get; init; } = true;

    /// <summary>Soft-delete (ADR D22). Default <c>true</c>.</summary>
    public bool IsAttivo { get; init; } = true;
}
