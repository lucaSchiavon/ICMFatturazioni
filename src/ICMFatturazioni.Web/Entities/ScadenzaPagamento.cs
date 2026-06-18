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

    /// <summary>Soft-delete (ADR D22). Default <c>true</c>.</summary>
    public bool IsAttivo { get; init; } = true;
}
