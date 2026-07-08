namespace ICMFatturazioni.Web.Models;

/// <summary>
/// Riga della griglia "Stampe fatture": una fattura ATTIVA arricchita con i dati
/// leggibili (cliente, tipo attività, attività) risolti tramite l'avviso di origine.
/// È un read-model di sola presentazione — non un'entità di dominio — perciò porta
/// già stringhe pronte per la UI invece degli Id.
/// </summary>
/// <param name="IdFattura">Chiave della fattura (per PDF ed eliminazione).</param>
/// <param name="IdAvviso">Avviso di origine (1:1), utile per navigazioni future.</param>
/// <param name="NumeroFattura">Numero progressivo nell'anno.</param>
/// <param name="Anno">Anno solare della fattura (chiave del filtro per anno).</param>
/// <param name="DataFattura">Data della fattura.</param>
/// <param name="ClienteRagioneSociale">Ragione sociale del cliente (colonna "Cliente").</param>
/// <param name="TipoAttivitaDescrizione">Descrizione del tipo attività (colonna "Tipo").</param>
/// <param name="NumeroAttivita">Codice/numero dell'attività (parte della colonna "Attività").</param>
/// <param name="DescrizioneAttivita">Descrizione dell'attività (parte della colonna "Attività").</param>
/// <param name="CreatoXML">True se la fattura ha già un tracciato XML: in tal caso non è eliminabile finché l'XML non viene rimosso.</param>
/// <param name="EsitoXML">Esito SdI dell'XML: 0 = attesa, 1 = OK (marcato come inviato).</param>
public sealed record FatturaEmessa(
    Guid     IdFattura,
    Guid     IdAvviso,
    int      NumeroFattura,
    int      Anno,
    DateOnly DataFattura,
    string   ClienteRagioneSociale,
    string?  TipoAttivitaDescrizione,
    string   NumeroAttivita,
    string   DescrizioneAttivita,
    bool     CreatoXML,
    int      EsitoXML);
