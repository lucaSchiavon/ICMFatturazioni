namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Descrizione standard richiamabile in inserimento (cap. 9.3 dispensa).
/// Non è vincolata via FK: è un catalogo di suggerimenti editabile dall'utente.
/// POCO senza dipendenze da Dapper/EF/ASP.NET.
/// </summary>
public sealed class DescrizioneAttivita
{
    /// <summary>Chiave primaria GUID (UUIDv7 app-side, ADR D22).</summary>
    public Guid IdDescrizioneAttivita { get; set; }

    /// <summary>Testo della descrizione. Obbligatorio e univoco tra gli attivi.</summary>
    public required string Descrizione { get; init; }

    /// <summary>Posizione nell'elenco (ordinamento «naturale» del lavoro).</summary>
    public int Ordine { get; init; }

    /// <summary>Soft-delete (ADR D22). Default <c>true</c>.</summary>
    public bool IsAttivo { get; init; } = true;
}
