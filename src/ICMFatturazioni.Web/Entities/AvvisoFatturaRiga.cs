namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Riga di un avviso di fattura (dispensa cap. 5-7).
///
/// Due nature:
/// <list type="bullet">
///   <item><b>Riga reale</b> (<see cref="IsDescrittiva"/> = false): "consuma" una
///   scadenza (<see cref="IdScadenza"/>) del suo dettaglio (<see cref="IdAttivitaDettaglio"/>)
///   e porta un <see cref="Importo"/>. La rata risulta così fatturata e viene
///   congelata in Gestione Scadenze (lock via <c>SchedulazionePagamenti.IdAvvisoRiga</c>).</item>
///   <item><b>Riga descrittiva</b> (<see cref="IsDescrittiva"/> = true): solo testo,
///   senza scadenza né importo (es. una nota inserita fra le voci).</item>
/// </list>
///
/// <see cref="Tipo"/> e <see cref="Descrizione"/> sono uno <b>snapshot</b> della
/// "Descrizione in Avviso" al momento dell'emissione: restano stabili anche se il
/// dettaglio d'origine viene poi rinominato.
/// </summary>
public sealed class AvvisoFatturaRiga
{
    /// <summary>Chiave primaria GUID (UUIDv7 app-side, ADR D22).</summary>
    public Guid IdRiga { get; set; }

    /// <summary>FK → fatt.AvvisiFattura: avviso di appartenenza.</summary>
    public Guid IdAvviso { get; set; }

    /// <summary>Posizione nella griglia (1-based). Assegnato e gestito dal Manager.</summary>
    public int Ordine { get; set; }

    /// <summary>
    /// FK → fatt.AttivitaDettaglio: dettaglio d'origine della rata. <c>null</c> per
    /// le righe descrittive.
    /// </summary>
    public Guid? IdAttivitaDettaglio { get; init; }

    /// <summary>
    /// FK → fatt.SchedulazionePagamenti: scadenza (rata) consumata da questa riga.
    /// <c>null</c> per le righe descrittive.
    /// </summary>
    public Guid? IdScadenza { get; init; }

    /// <summary>Snapshot del tipo dettaglio ("Descrizione in Avviso"). Facoltativo.</summary>
    public string? Tipo { get; init; }

    /// <summary>Snapshot del testo mostrato in avviso. Obbligatorio.</summary>
    public required string Descrizione { get; init; }

    /// <summary>Importo della rata (€). <c>null</c> per le righe descrittive.</summary>
    public decimal? Importo { get; init; }

    /// <summary>True se la riga è puramente descrittiva (niente scadenza né importo).</summary>
    public bool IsDescrittiva { get; init; }
}
