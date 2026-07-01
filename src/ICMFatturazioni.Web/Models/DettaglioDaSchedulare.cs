namespace ICMFatturazioni.Web.Models;

/// <summary>
/// Dettaglio di un'attività il cui importo <b>non è ancora interamente schedulato</b>
/// in scadenze (somma delle rate attive &lt; importo del dettaglio, incluso il caso di
/// zero scadenze). Non è fatturabile dalla maschera Avvisi finché non si pianificano
/// le scadenze in Gestione Attività: la maschera lo segnala per evitare "buchi"
/// (importo non fatturato che sfugge perché privo di rate).
/// </summary>
/// <param name="Descrizione">Descrizione del dettaglio.</param>
/// <param name="ImportoNonSchedulato">Quota dell'importo del dettaglio non ancora coperta da scadenze.</param>
public sealed record DettaglioDaSchedulare(string Descrizione, decimal ImportoNonSchedulato);
