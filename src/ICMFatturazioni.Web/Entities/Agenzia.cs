namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Filiale/agenzia di una <see cref="Banca"/> (anagrafica). POCO senza
/// dipendenze da Dapper/EF/ASP.NET.
/// </summary>
/// <remarks>
/// L'agenzia appartiene a una banca (<see cref="IdBanca"/>). Il <see cref="CAB"/>
/// identifica la filiale (5 cifre) ed è costante per agenzia: un'agenzia ha un
/// solo CAB. Il <see cref="Nome"/> è univoco per banca tra gli attivi.
/// </remarks>
public sealed class Agenzia
{
    /// <summary>Chiave primaria GUID (UUIDv7 app-side, ADR D22).</summary>
    public Guid IdAgenzia { get; set; }

    /// <summary>Banca di appartenenza (FK → <c>fatt.Banche.IdBanca</c>).</summary>
    public required Guid IdBanca { get; init; }

    /// <summary>Nome della filiale. Obbligatorio e univoco per banca tra gli attivi.</summary>
    public required string Nome { get; init; }

    /// <summary>Codice CAB (5 cifre). Nullable: può essere ignoto.</summary>
    public string? CAB { get; init; }

    /// <summary>Soft-delete (ADR D22). Default <c>true</c>.</summary>
    public bool IsAttivo { get; init; } = true;
}
