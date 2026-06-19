namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Testata di un'attività cliente (dispensa cap. 10.2).
/// Regola date: <see cref="ProgettoDefinitivo"/> ≤ <see cref="ConcessioneEdilizia"/> ≤ <see cref="InizioLavori"/>
/// (enforced dal Manager, non da CHECK SQL per evitare conflitti con NULL).
/// Soft-delete: <see cref="IsAttivo"/>.
/// </summary>
public sealed class Attivita
{
    /// <summary>Chiave primaria GUID (UUIDv7 app-side, ADR D22).</summary>
    public Guid IdAttivita { get; set; }

    /// <summary>FK → fatt.Anagrafica: il cliente/committente.</summary>
    public Guid IdAnagrafica { get; set; }

    /// <summary>FK → fatt.TipiAttivita: tipo (CONSULENZE, PROGETTAZIONI, ALTRO).</summary>
    public Guid IdTipoAttivita { get; set; }

    /// <summary>
    /// Numero/codice identificativo dell'attività, inputtabile dall'utente
    /// (dispensa cap. 10.2: "numero o nome mnemonico"). Tipizzato come stringa
    /// dopo la fusione col DB unificato: e' il medesimo campo di
    /// dbo.Progetto.Codice in ICMVerbali (es. "WHA-WHD"), esposto come "Numero"
    /// dalla vista fatt.Attivita.
    /// </summary>
    public string Numero { get; set; } = string.Empty;

    /// <summary>Descrizione libera dell'attività. Obbligatoria.</summary>
    public required string Descrizione { get; init; }

    /// <summary>Prima data del flusso (deve precedere ConcessioneEdilizia e InizioLavori).</summary>
    public DateOnly? ProgettoDefinitivo { get; init; }

    /// <summary>Data concessione edilizia (deve seguire ProgettoDefinitivo e precedere InizioLavori).</summary>
    public DateOnly? ConcessioneEdilizia { get; init; }

    /// <summary>Data inizio lavori (ultima del flusso, deve seguire ConcessioneEdilizia).</summary>
    public DateOnly? InizioLavori { get; init; }

    /// <summary>Costo dell'opera commissionata — distinto dal compenso dello studio. Facoltativo.</summary>
    public decimal? ImportoOpera { get; init; }

    /// <summary>Soft-delete (ADR D22). Default <c>true</c>.</summary>
    public bool IsAttivo { get; init; } = true;

    // Proprietà di navigazione (non mappate sul DB — popolate da join nel Repository).

    /// <summary>Ragione sociale dell'anagrafica (join di convenienza).</summary>
    public string? RagioneSociale { get; set; }

    /// <summary>Descrizione del tipo attività (join di convenienza).</summary>
    public string? TipoAttivitaDescrizione { get; set; }
}
