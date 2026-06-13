namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Istituto bancario (anagrafica). POCO senza dipendenze da Dapper/EF/ASP.NET.
/// </summary>
/// <remarks>
/// L'<see cref="ABI"/> identifica la banca a livello nazionale (5 cifre): è
/// costante per istituto. Il <see cref="Nome"/> è la "chiave umana" (univoca
/// tra gli attivi) usata dalla combo. Le filiali sono in <see cref="Agenzia"/>.
/// </remarks>
public sealed class Banca
{
    /// <summary>Chiave primaria GUID (UUIDv7 app-side, ADR D22).</summary>
    public Guid IdBanca { get; set; }

    /// <summary>Nome dell'istituto. Obbligatorio e univoco tra gli attivi.</summary>
    public required string Nome { get; init; }

    /// <summary>Codice ABI (5 cifre). Nullable: può essere ignoto.</summary>
    public string? ABI { get; init; }

    /// <summary>Soft-delete (ADR D22). Default <c>true</c>.</summary>
    public bool IsAttivo { get; init; } = true;
}
