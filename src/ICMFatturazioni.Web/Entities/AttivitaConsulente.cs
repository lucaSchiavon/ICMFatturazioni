namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Riga di consulenza esterna agganciata a un'attività cliente (Modulo Attività
/// Consulenti, dispensa cap. 3): quale consulente, per quale specializzazione,
/// a carico di chi, a che compenso e con quale scadenza-promemoria.
/// Il pagato/residuo NON è memorizzato: si deriva sempre dalle tranche attive di
/// fatt.AttivitaConsulentiPagamenti (Importo − Σ pagamenti).
/// Soft-delete: <see cref="IsAttivo"/>. POCO senza dipendenze da Dapper/EF/ASP.NET.
/// </summary>
public sealed class AttivitaConsulente
{
    /// <summary>Chiave primaria GUID (UUIDv7 app-side, ADR D22).</summary>
    public Guid IdAttivitaConsulente { get; set; }

    /// <summary>FK → attività cliente (fisicamente dbo.Progetto, vedi migration 077).</summary>
    public Guid IdAttivita { get; set; }

    /// <summary>FK → fatt.Consulenti: il collaboratore esterno.</summary>
    public Guid IdConsulente { get; set; }

    /// <summary>FK → fatt.TipiAttivitaConsulenti: la specializzazione della consulenza.</summary>
    public Guid IdTipoAttivitaConsulente { get; set; }

    /// <summary>Bivio Studio/Cliente (dispensa cap. 4): decide la sotto-griglia e
    /// se la riga è soggetta a gestione pagamenti.</summary>
    public CaricoConsulenza Carico { get; init; } = CaricoConsulenza.Studio;

    /// <summary>Compenso pattuito con il consulente per questa attività.</summary>
    public decimal Importo { get; init; }

    /// <summary>Data-promemoria di quando pagare il consulente. Facoltativa.</summary>
    public DateOnly? Scadenza { get; init; }

    /// <summary>Annotazione libera sulla riga. Facoltativa.</summary>
    public string? Nota { get; init; }

    /// <summary>Soft-delete (ADR D22). Default <c>true</c>.</summary>
    public bool IsAttivo { get; init; } = true;

    // Proprietà di navigazione (non mappate sul DB — popolate da join nel Repository).

    /// <summary>Denominazione del consulente (join di convenienza).</summary>
    public string? ConsulenteDescrizione { get; set; }

    /// <summary>Descrizione del tipo attività consulente (join di convenienza).</summary>
    public string? TipoAttivitaConsulenteDescrizione { get; set; }
}
