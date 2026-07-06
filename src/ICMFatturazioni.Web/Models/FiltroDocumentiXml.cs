namespace ICMFatturazioni.Web.Models;

/// <summary>
/// Stato di creazione del tracciato XML, per il filtro della maschera
/// "Creazione-Gestione XML Documenti".
/// </summary>
public enum StatoCreazioneXml
{
    /// <summary>Solo fatture il cui XML non è ancora stato creato (<c>CreatoXML = 0</c>).</summary>
    DaCreare,
    /// <summary>Solo fatture il cui XML è già stato creato (<c>CreatoXML = 1</c>).</summary>
    Creato,
    /// <summary>Nessun filtro sullo stato di creazione.</summary>
    Tutti,
}

/// <summary>
/// Stato dell'esito dell'invio allo SdI, per il filtro della maschera.
/// </summary>
public enum StatoEsitoXml
{
    /// <summary>Solo fatture in attesa di esito (<c>EsitoXML = 0</c>).</summary>
    Attesa,
    /// <summary>Solo fatture con esito confermato OK (<c>EsitoXML = 1</c>).</summary>
    Ok,
    /// <summary>Nessun filtro sull'esito.</summary>
    Tutti,
}

/// <summary>
/// Filtro della griglia della maschera "Creazione-Gestione XML Documenti"
/// (spec <c>CreazioneGestioneXml.pdf</c>). L'anno guida i default delle date, ma è
/// l'intervallo <see cref="DataDa"/>–<see cref="DataA"/> a filtrare effettivamente
/// (le date sono sovrascrivibili dall'utente).
/// </summary>
/// <param name="IdAnagrafica">Cliente selezionato; <c>null</c> = tutte le anagrafiche.</param>
/// <param name="DataDa">Estremo inferiore (incluso) del range su <c>DataFattura</c>.</param>
/// <param name="DataA">Estremo superiore (incluso) del range su <c>DataFattura</c>.</param>
/// <param name="Creazione">Filtro sullo stato di creazione dell'XML.</param>
/// <param name="Esito">Filtro sullo stato dell'esito dell'invio.</param>
public sealed record FiltroDocumentiXml(
    Guid?             IdAnagrafica,
    DateOnly          DataDa,
    DateOnly          DataA,
    StatoCreazioneXml Creazione,
    StatoEsitoXml     Esito);
