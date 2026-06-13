namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Tipo di pagamento del catalogo amministrativo (dispensa cap. 3, livello
/// "padre" della gerarchia tipo→codice). POCO senza dipendenze da Dapper/EF/ASP.NET.
/// </summary>
/// <remarks>
/// Il <see cref="FlagBanca"/> è la regola di dominio: decide di chi sono i dati
/// bancari in fattura (Azienda per i bonifici, Cliente per le ricevute bancarie).
/// La proprietà descrittiva è <see cref="Descrizione"/> (sul DB colonna
/// <c>TipoPagamento</c>, NVARCHAR(50)): il nome C# diverge dalla colonna perché
/// non può coincidere col nome della classe.
/// </remarks>
public sealed class TipoPagamento
{
    /// <summary>Chiave primaria GUID (UUIDv7 app-side, ADR D22).</summary>
    public Guid IdTipoPagamento { get; set; }

    /// <summary>
    /// Descrizione del tipo (es. "Bonifico"). Obbligatoria e univoca tra gli
    /// attivi. Mappata sulla colonna legacy <c>TipoPagamento</c>.
    /// </summary>
    public required string Descrizione { get; init; }

    /// <summary>Sigla breve (es. "BO", "RB"). Facoltativa, univoca tra gli attivi.</summary>
    public string? SiglaPag { get; init; }

    /// <summary>Flag banca (Azienda/Cliente), persistito come <c>CHAR(1)</c>.</summary>
    public required FlagBanca FlagBanca { get; init; }

    /// <summary>Soft-delete (ADR D22). Default <c>true</c>.</summary>
    public bool IsAttivo { get; init; } = true;
}
