using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Models;

/// <summary>
/// Read-model di una riga del report "Scadenziario attività clienti"
/// (maschera "Stampa scadenze"): la scadenza arricchita con cliente,
/// attività, dettaglio e stato di evasione. Alimenta sia l'anteprima live
/// della maschera (conteggio + totale) sia il rendering PDF.
/// </summary>
/// <param name="DataScadenza">Data della rata (colonna SCADENZA del report).</param>
/// <param name="Importo">Importo della rata (si fattura intera: importo pieno anche se evasa).</param>
/// <param name="IsEvasa">True se la rata è consumata da un avviso di fattura attivo.</param>
/// <param name="AvvisoDataEvasione">Data dell'avviso che ha evaso la rata (null se non evasa).</param>
/// <param name="NotaScadenza">Nota della rata (es. "acconto 30%"); annotata sotto il dettaglio.</param>
/// <param name="TipoCliente">Tipologia del cliente (colonna T: S/P/E).</param>
/// <param name="ClienteRagioneSociale">Ragione sociale del cliente.</param>
/// <param name="TipoAttivitaDescrizione">Tipo dell'attività (es. "PROGETTAZIONI"); null se orfano.</param>
/// <param name="NumeroAttivita">Numero/codice attività (stringa, vista su dbo.Progetto.Codice).</param>
/// <param name="DescrizioneAttivita">Descrizione dell'attività.</param>
/// <param name="TipoDettaglioDescrizione">Tipo del dettaglio (es. "DISCIPLINARE"); null se orfano.</param>
/// <param name="DescrizioneDettaglio">Descrizione della riga dettaglio di appartenenza.</param>
public sealed record ScadenzaReport(
    DateOnly       DataScadenza,
    decimal        Importo,
    bool           IsEvasa,
    DateOnly?      AvvisoDataEvasione,
    string?        NotaScadenza,
    TipoAnagrafica TipoCliente,
    string         ClienteRagioneSociale,
    string?        TipoAttivitaDescrizione,
    string         NumeroAttivita,
    string         DescrizioneAttivita,
    string?        TipoDettaglioDescrizione,
    string         DescrizioneDettaglio);
