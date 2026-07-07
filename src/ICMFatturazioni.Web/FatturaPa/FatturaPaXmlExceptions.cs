namespace ICMFatturazioni.Web.FatturaPa;

/// <summary>
/// La fattura richiesta per la generazione XML non esiste o è stata annullata.
/// Flusso previsto (→ 404): non va loggata (Regola 6).
/// </summary>
public sealed class FatturaPaXmlNonTrovataException : Exception
{
    public Guid IdFattura { get; }
    public FatturaPaXmlNonTrovataException(Guid idFattura)
        : base($"Fattura {idFattura} non trovata o annullata.") => IdFattura = idFattura;
}

/// <summary>
/// Dati indispensabili mancanti/incoerenti per comporre il tracciato (es. azienda
/// emittente senza P.IVA, cliente senza codice fiscale/P.IVA). È un errore di
/// configurazione dei dati: <b>va loggato</b>.
/// </summary>
public sealed class FatturaPaDatiMancantiException : Exception
{
    public FatturaPaDatiMancantiException(string message) : base(message) { }
}

/// <summary>
/// Il tracciato generato non ha superato la validazione offline della libreria
/// (schema/regole FatturaPA). Espone l'elenco dei problemi da mostrare all'utente:
/// è un esito di validazione, non un errore da loggare.
/// </summary>
public sealed class FatturaPaXmlNonValidoException : Exception
{
    /// <summary>Elenco leggibile degli errori di validazione ("Campo: messaggio").</summary>
    public IReadOnlyList<string> Errori { get; }

    public FatturaPaXmlNonValidoException(IReadOnlyList<string> errori)
        : base("Il tracciato XML non è valido: " + string.Join(" · ", errori))
        => Errori = errori;
}

/// <summary>
/// Si è richiesto il download del tracciato di una fattura il cui XML non è ancora
/// stato generato. Flusso previsto (→ 409): non va loggato.
/// </summary>
public sealed class FatturaPaXmlNonGeneratoException : Exception
{
    public Guid IdFattura { get; }
    public FatturaPaXmlNonGeneratoException(Guid idFattura)
        : base($"Il tracciato XML della fattura {idFattura} non è ancora stato generato.")
        => IdFattura = idFattura;
}
