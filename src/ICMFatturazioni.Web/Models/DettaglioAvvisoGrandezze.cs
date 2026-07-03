namespace ICMFatturazioni.Web.Models;

/// <summary>
/// Riga informativa del pannello "Dettagli Attività" nella maschera Emissione
/// Fatture, per un dettaglio presente nell'avviso selezionato. Espone le quattro
/// grandezze del modello Access:
/// <list type="bullet">
///   <item><b>Importo</b> — importo totale del dettaglio.</item>
///   <item><b>Altri Avv.</b> — quota già allocata in ALTRI avvisi attivi.</item>
///   <item><b>Avv. att.</b> — quota allocata nell'avviso corrente.</item>
///   <item><b>Residuo</b> — importo dettaglio − altri avvisi − avviso attuale.</item>
/// </list>
/// </summary>
/// <param name="IdAttivitaDettaglio">Dettaglio dell'attività.</param>
/// <param name="Tipo">Descrizione del tipo di dettaglio.</param>
/// <param name="Descrizione">Descrizione del dettaglio.</param>
/// <param name="DataScadenza">Prima scadenza del dettaglio consumata da questo avviso (informativa).</param>
/// <param name="ImportoDettaglio">Importo totale del dettaglio.</param>
/// <param name="AltriAvvisi">Quota allocata in altri avvisi attivi.</param>
/// <param name="AvvisoAttuale">Quota allocata nell'avviso corrente.</param>
public sealed record DettaglioAvvisoGrandezze(
    Guid      IdAttivitaDettaglio,
    string?   Tipo,
    string    Descrizione,
    DateOnly? DataScadenza,
    decimal   ImportoDettaglio,
    decimal   AltriAvvisi,
    decimal   AvvisoAttuale)
{
    /// <summary>Residuo del dettaglio dopo gli avvisi (può essere 0).</summary>
    public decimal Residuo => ImportoDettaglio - AltriAvvisi - AvvisoAttuale;
}
