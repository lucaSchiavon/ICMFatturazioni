namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Tipo di attività dei consulenti esterni (Modulo Attività Consulenti). Catalogo
/// amministrativo con soft-delete (IsAttivo), gemello di <see cref="TipoAttivita"/>
/// ma per le consulenze esterne. POCO senza dipendenze da Dapper/EF/ASP.NET.
/// </summary>
public sealed class TipoAttivitaConsulente
{
    /// <summary>Chiave primaria GUID (UUIDv7 app-side, ADR D22).</summary>
    public Guid IdTipoAttivitaConsulente { get; set; }

    /// <summary>Descrizione del tipo (es. "CALCOLI STRUTTURALI"). Obbligatoria e univoca tra gli attivi.
    /// Corrisponde alla colonna SQL <c>TipoAttivitaConsulente</c> (nome fedele al legacy, ADR D1).</summary>
    public required string Descrizione { get; init; }

    /// <summary>Soft-delete (ADR D22). Default <c>true</c>.</summary>
    public bool IsAttivo { get; init; } = true;
}
