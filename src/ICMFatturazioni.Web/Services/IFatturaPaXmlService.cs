namespace ICMFatturazioni.Web.Services;

/// <summary>
/// Esito della generazione di un tracciato XML FatturaPA.
/// </summary>
/// <param name="NomeFile">Nome del file prodotto (convenzione FatturaPA).</param>
/// <param name="PercorsoCompleto">Percorso completo del file salvato su disco.</param>
/// <param name="ProgressivoInvio">Progressivo invio (5 char) assegnato/riusato.</param>
public sealed record GenerazioneXmlRisultato(
    string NomeFile,
    string PercorsoCompleto,
    string ProgressivoInvio);

/// <summary>
/// Genera il tracciato XML FatturaPA (formato <b>FPR12</b>, privati/società) a
/// partire da una fattura di cortesia già emessa (Fase D1). Il servizio si occupa
/// SOLO della produzione del file: mappatura dei dati sul tracciato, validazione
/// offline, serializzazione, salvataggio nella cartella configurata e marcatura di
/// stato. L'invio allo SdI è esterno all'applicazione.
/// </summary>
public interface IFatturaPaXmlService
{
    /// <summary>
    /// Genera (o rigenera) il tracciato XML della fattura, lo valida offline, lo
    /// salva nella cartella configurata e marca la fattura come "XML creato".
    /// Alla prima generazione assegna un nuovo progressivo invio; sulle
    /// rigenerazioni riusa quello già persistito (stesso nome file).
    /// </summary>
    /// <exception cref="ICMFatturazioni.Web.FatturaPa.FatturaPaXmlNonTrovataException">Fattura inesistente/annullata.</exception>
    /// <exception cref="ICMFatturazioni.Web.FatturaPa.FatturaPaEntePubblicoException">Cliente ente pubblico (FPR12 non applicabile).</exception>
    /// <exception cref="ICMFatturazioni.Web.FatturaPa.FatturaPaDatiMancantiException">Dati azienda/cliente indispensabili mancanti.</exception>
    /// <exception cref="ICMFatturazioni.Web.FatturaPa.FatturaPaXmlNonValidoException">Il tracciato non supera la validazione offline.</exception>
    Task<GenerazioneXmlRisultato> GeneraAsync(Guid idFattura, CancellationToken ct = default);

    /// <summary>
    /// Restituisce il contenuto del tracciato XML per il download. Se il file è
    /// presente nella cartella configurata lo legge; se manca (spostato/eliminato) lo
    /// rigenera al volo dai dati, riusando il progressivo già persistito (nessun
    /// consumo di sequence, nessun cambio di stato).
    /// </summary>
    /// <exception cref="ICMFatturazioni.Web.FatturaPa.FatturaPaXmlNonTrovataException">Fattura inesistente/annullata.</exception>
    /// <exception cref="ICMFatturazioni.Web.FatturaPa.FatturaPaXmlNonGeneratoException">XML mai generato per questa fattura.</exception>
    Task<(byte[] Contenuto, string NomeFile)> ScaricaAsync(Guid idFattura, CancellationToken ct = default);
}
