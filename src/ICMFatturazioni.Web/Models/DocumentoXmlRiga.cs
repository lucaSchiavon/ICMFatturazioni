using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Models;

/// <summary>
/// Riga della griglia della maschera "Creazione-Gestione XML Documenti": una
/// fattura ATTIVA con lo stato del suo tracciato elettronico e i dati leggibili
/// (cliente, tipo cliente, attività) risolti tramite l'avviso di origine.
/// Read-model di sola presentazione: porta stringhe/flag pronti per la UI.
/// </summary>
/// <param name="IdFattura">Chiave della fattura (per generazione XML, PDF, download).</param>
/// <param name="NumeroFattura">Numero progressivo della fattura nell'anno (colonna "N.").</param>
/// <param name="Anno">Anno solare della fattura.</param>
/// <param name="DataFattura">Data della fattura (colonna "Del").</param>
/// <param name="TipoAnagrafica">Tipo cliente S/P/E (colonna "*").</param>
/// <param name="ClienteRagioneSociale">Ragione sociale del cliente (colonna "Cliente").</param>
/// <param name="TipoAttivitaDescrizione">Descrizione del tipo attività.</param>
/// <param name="NumeroAttivita">Codice/numero dell'attività.</param>
/// <param name="DescrizioneAttivita">Descrizione dell'attività.</param>
/// <param name="CreatoXML">True se il tracciato XML è già stato generato (colonna "Creato").</param>
/// <param name="EsitoXML">Esito invio SdI: 0 = attesa, 1 = OK (colonna "Esito OK").</param>
/// <param name="ProgressivoInvio">Progressivo invio del file, se generato.</param>
/// <param name="NomeFileXml">Nome del file XML prodotto, se generato (per il link di download).</param>
public sealed record DocumentoXmlRiga(
    Guid           IdFattura,
    int            NumeroFattura,
    int            Anno,
    DateOnly       DataFattura,
    TipoAnagrafica TipoAnagrafica,
    string         ClienteRagioneSociale,
    string?        TipoAttivitaDescrizione,
    string         NumeroAttivita,
    string         DescrizioneAttivita,
    bool           CreatoXML,
    int            EsitoXML,
    string?        ProgressivoInvio,
    string?        NomeFileXml)
{
    /// <summary>True se l'esito dell'invio è stato confermato OK.</summary>
    public bool EsitoOk => EsitoXML == 1;
}
