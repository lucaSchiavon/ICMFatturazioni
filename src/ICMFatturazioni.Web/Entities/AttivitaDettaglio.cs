namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Riga di dettaglio di una testata attività (dispensa cap. 10.3).
/// L'ordine di visualizzazione è gestito dal Manager (1-based).
/// <see cref="HasFattura"/> è read-only per il gestionale attività:
/// viene impostato true dal modulo fatturazione quando la riga è emessa.
/// </summary>
public sealed class AttivitaDettaglio
{
    /// <summary>Chiave primaria GUID (UUIDv7 app-side, ADR D22).</summary>
    public Guid IdAttivitaDettaglio { get; set; }

    /// <summary>FK → fatt.Attivita: testata di appartenenza.</summary>
    public Guid IdAttivita { get; set; }

    /// <summary>FK → fatt.TipiDettaglioAttivita (DISCIPLINARE, EXTRA DISCIPLINARE, ecc.).</summary>
    public Guid IdTipoDettaglioAttivita { get; set; }

    /// <summary>Posizione nella griglia (1-based). Assegnato e gestito dal Manager.</summary>
    public int Ordine { get; set; }

    /// <summary>Descrizione del dettaglio. Obbligatoria.</summary>
    public required string DescrizioneDettaglio { get; init; }

    /// <summary>Importo della voce (€). Deve essere &gt; 0.</summary>
    public decimal Importo { get; init; }

    /// <summary>Nota libera — es. "(QUOTA PARTE DI € 1.000,00)". Mostrata in griglia.</summary>
    public string? NotaDettaglio { get; init; }

    /// <summary>Data di scadenza prevista per questa voce. Obbligatoria
    /// (validata nel Manager; il tipo resta nullable perché il form parte vuoto).</summary>
    public DateOnly? TerminePrevisto { get; init; }

    /// <summary>
    /// True se la riga è inclusa in una fattura emessa (impostato dal modulo fatturazione).
    /// Quando true: la riga non può essere eliminata né le sue scadenze modificate.
    /// </summary>
    public bool HasFattura { get; init; }

    /// <summary>Soft-delete (ADR D22). Default <c>true</c>.</summary>
    public bool IsAttivo { get; init; } = true;

    // Proprietà di navigazione (non mappate sul DB — popolate da join nel Repository).

    /// <summary>Descrizione del tipo dettaglio (join di convenienza).</summary>
    public string? TipoDettaglioDescrizione { get; set; }

    /// <summary>Numero di scadenze attive (subquery nel Repository).</summary>
    public int NumeroScadenze { get; set; }

    /// <summary>Somma degli importi delle scadenze attive (subquery nel Repository).</summary>
    public decimal TotaleScadenzato { get; set; }
}
