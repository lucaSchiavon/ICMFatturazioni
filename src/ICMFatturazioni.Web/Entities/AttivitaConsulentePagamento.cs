namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Tranche di pagamento di una riga consulenza a carico dello Studio (Modulo
/// Attività Consulenti, dispensa cap. 5). Il saldo funziona per tranche
/// successive: residuo = Importo riga − Σ tranche attive, fino a zero.
/// Esiste SOLO per righe con Carico = Studio (dispensa cap. 4).
/// Soft-delete: <see cref="IsAttivo"/>. POCO senza dipendenze da Dapper/EF/ASP.NET.
/// </summary>
public sealed class AttivitaConsulentePagamento
{
    /// <summary>Chiave primaria GUID (UUIDv7 app-side, ADR D22).</summary>
    public Guid IdConsulentePagamento { get; set; }

    /// <summary>FK → fatt.AttivitaConsulenti: la riga consulenza saldata da questa tranche.</summary>
    public Guid IdAttivitaConsulente { get; set; }

    /// <summary>Data della tranche (legacy "DataPagamento"). Obbligatoria.</summary>
    public DateOnly DataPagamento { get; init; }

    /// <summary>Importo della tranche. Positivo e mai oltre il residuo (D-C3).</summary>
    public decimal Importo { get; init; }

    /// <summary>Annotazione libera sulla tranche. Facoltativa.</summary>
    public string? Nota { get; init; }

    /// <summary>Soft-delete (ADR D22). Default <c>true</c>.</summary>
    public bool IsAttivo { get; init; } = true;
}
