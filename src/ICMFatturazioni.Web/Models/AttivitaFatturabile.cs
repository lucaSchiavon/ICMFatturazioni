namespace ICMFatturazioni.Web.Models;

/// <summary>
/// Attività che ha ancora almeno una scadenza <b>fatturabile</b> (rata attiva non
/// consumata da alcun avviso). Alimenta i filtri della maschera Avvisi di fattura:
/// un'attività i cui importi sono interamente coperti dagli avvisi (o priva di
/// scadenze schedulate) non è più fatturabile qui e va nascosta; un cliente senza
/// alcuna attività fatturabile non compare nemmeno nel selettore anagrafiche
/// (dispensa: "le attività coperte spariscono dal filtro").
/// </summary>
/// <param name="IdAnagrafica">Cliente dell'attività (per filtrare il selettore anagrafiche).</param>
/// <param name="IdAttivita">Attività ancora fatturabile (per filtrare il selettore attività).</param>
public sealed record AttivitaFatturabile(Guid IdAnagrafica, Guid IdAttivita);
