namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Scadenza di pagamento di una riga dettaglio attività (dispensa cap. 10.4).
/// Una riga dettaglio può avere N scadenze; la somma degli importi dovrebbe
/// corrispondere all'importo della riga (verifica best-effort nella UI).
/// </summary>
public sealed class ScadenzaPagamento
{
    /// <summary>Chiave primaria GUID (UUIDv7 app-side, ADR D22).</summary>
    public Guid IdScadenza { get; set; }

    /// <summary>FK → fatt.AttivitaDettaglio: riga di appartenenza.</summary>
    public Guid IdAttivitaDettaglio { get; set; }

    /// <summary>Data di scadenza del pagamento. Obbligatoria.</summary>
    public DateOnly DataScadenza { get; init; }

    /// <summary>Importo della scadenza (€). Deve essere &gt; 0.</summary>
    public decimal Importo { get; init; }

    /// <summary>Nota libera (es. "acconto 30%"). Facoltativa.</summary>
    public string? Nota { get; init; }

    /// <summary>
    /// FK → fatt.AvvisoFatturaRighe: riga-avviso che ha "consumato" (evaso) questa
    /// rata. <c>null</c> = rata ancora da evadere. Quando valorizzata la scadenza è
    /// <b>congelata</b> in Gestione Scadenze (lock a livello rata): niente modifica,
    /// eliminazione o ri-suddivisione finché non si annulla l'avviso che la consuma.
    /// </summary>
    public Guid? IdAvvisoRiga { get; init; }

    /// <summary>True se la rata è già evasa da un avviso (derivato da <see cref="IdAvvisoRiga"/>).</summary>
    public bool IsEvasa => IdAvvisoRiga.HasValue;

    /// <summary>Soft-delete (ADR D22). Default <c>true</c>.</summary>
    public bool IsAttivo { get; init; } = true;

    // --- Nav read-only (non mappate su colonne): valorizzate solo dalla lettura
    //     per-dettaglio, per mostrare in UI in quale avviso la rata è stata evasa. ---

    /// <summary>Data dell'avviso che ha evaso la rata (null se non evasa).</summary>
    public DateOnly? AvvisoDataEvasione { get; set; }

    /// <summary>Oggetto dell'avviso che ha evaso la rata (null se non evasa o senza oggetto).</summary>
    public string? AvvisoOggettoEvasione { get; set; }
}
