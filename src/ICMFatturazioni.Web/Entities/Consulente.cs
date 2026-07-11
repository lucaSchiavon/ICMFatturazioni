namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Consulente esterno dello studio (Modulo Attività Consulenti, dispensa cap. 1-2).
/// Anagrafica semplice con soft-delete (IsAttivo). POCO senza dipendenze da Dapper/EF/ASP.NET.
/// </summary>
public sealed class Consulente
{
    /// <summary>Chiave primaria GUID (UUIDv7 app-side, ADR D22).</summary>
    public Guid IdConsulente { get; set; }

    /// <summary>Denominazione del consulente (es. "Luca Schiavon"). Obbligatoria e univoca tra gli attivi.
    /// Corrisponde alla colonna SQL <c>Consulente</c> (nome fedele al legacy, ADR D1).</summary>
    public required string Descrizione { get; init; }

    /// <summary>Soft-delete (ADR D22). Default <c>true</c>.</summary>
    public bool IsAttivo { get; init; } = true;
}
