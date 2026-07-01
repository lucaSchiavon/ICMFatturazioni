namespace ICMFatturazioni.Web.Models;

/// <summary>
/// Read-model di una scadenza (rata) ancora <b>fatturabile</b> di un'attività:
/// una rata attiva, non ancora consumata da alcun avviso, arricchita con i dati
/// del dettaglio d'origine e con quanto già allocato negli avvisi.
///
/// Alimenta la griglia di selezione dell'avviso (guidata dalle scadenze, memoria
/// avviso-fattura-design) e fornisce al Manager gli importi/etichette
/// <b>autorevoli</b> con cui costruire le righe in fase di emissione (la rata si
/// fattura intera: l'importo è quello del DB, non digitato dall'utente).
/// </summary>
/// <param name="IdScadenza">PK della scadenza.</param>
/// <param name="IdAttivitaDettaglio">Dettaglio d'origine (per il raggruppamento).</param>
/// <param name="DataScadenza">Data di scadenza della rata.</param>
/// <param name="Importo">Importo autorevole della rata (si fattura intera).</param>
/// <param name="Nota">Nota della scadenza.</param>
/// <param name="OrdineDettaglio">Ordine del dettaglio nella griglia attività (raggruppamento).</param>
/// <param name="IdTipoDettaglioAttivita">Tipo del dettaglio.</param>
/// <param name="TipoDettaglioDescrizione">Descrizione del tipo (snapshot "Tipo in Avviso").</param>
/// <param name="DescrizioneDettaglio">Descrizione del dettaglio (snapshot "Descrizione in Avviso").</param>
/// <param name="ImportoDettaglio">Importo totale del dettaglio ("importo iniziale" della riga).</param>
/// <param name="GiaAllocatoAvvisiPrecedenti">
/// Somma degli importi delle righe di avvisi <b>attivi</b> per lo stesso dettaglio
/// (quanto già messo in avviso). La quota dell'avviso corrente in bozza — non ancora
/// persistito — è calcolata live dalla UI e sommata a questa base.
/// </param>
public sealed record ScadenzaFatturabile(
    Guid     IdScadenza,
    Guid     IdAttivitaDettaglio,
    DateOnly DataScadenza,
    decimal  Importo,
    string?  Nota,
    int      OrdineDettaglio,
    Guid     IdTipoDettaglioAttivita,
    string?  TipoDettaglioDescrizione,
    string   DescrizioneDettaglio,
    decimal  ImportoDettaglio,
    decimal  GiaAllocatoAvvisiPrecedenti);
