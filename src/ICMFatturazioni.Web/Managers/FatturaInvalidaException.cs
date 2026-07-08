namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Motivo per cui una <see cref="FatturaInvalidaException"/> è stata lanciata,
/// durante la creazione/annullamento di una fattura da un avviso.
/// </summary>
public enum FatturaMotivoInvalido
{
    /// <summary>L'avviso indicato non esiste o è stato annullato.</summary>
    AvvisoNonTrovato,

    /// <summary>
    /// L'avviso ha già una fattura attiva (violazione dell'indice univoco filtrato
    /// <c>UQ_Fatture_IdAvviso_Attiva</c>). Guardia anti doppia-fatturazione.
    /// </summary>
    AvvisoGiaFatturato,

    /// <summary>La data della fattura non è stata valorizzata.</summary>
    DataObbligatoria,

    /// <summary>Il numero di fattura non è valido (deve essere &gt; 0).</summary>
    NumeroNonValido,

    /// <summary>
    /// Il numero indicato è già usato da un'altra fattura attiva dello stesso anno
    /// (violazione di <c>UQ_Fatture_Anno_Numero_Attiva</c>). Riproporre il successivo.
    /// </summary>
    NumeroDuplicato,

    /// <summary>La fattura indicata non esiste o è stata annullata.</summary>
    FatturaNonTrovata,

    /// <summary>
    /// Si è tentato di confermare l'esito SdI di una fattura il cui tracciato XML
    /// non è ancora stato generato (Fase D1): prima si crea l'XML, poi si conferma.
    /// </summary>
    XmlNonCreato,

    /// <summary>
    /// Si è tentato di eliminare una fattura che ha già un tracciato XML generato.
    /// Va prima rimosso l'XML (dalla maschera Documenti XML), poi la fattura:
    /// così l'ordine di eliminazione è simmetrico a quello di creazione.
    /// </summary>
    FatturaConXmlNonEliminabile,

    /// <summary>
    /// Si è tentato di eliminare il tracciato XML di una fattura il cui esito è già
    /// stato confermato OK (segnata come inviata allo SdI): prima si toglie l'esito,
    /// poi si può eliminare l'XML.
    /// </summary>
    XmlConEsitoConfermato,

    /// <summary>
    /// Numero e data della nuova fattura non sono coerenti con la sequenza delle
    /// fatture già esistenti nell'anno: la numerazione progressiva deve seguire
    /// l'ordine cronologico (a numero maggiore non può corrispondere data anteriore).
    /// </summary>
    SequenzaDataNumeroIncoerente,
}

/// <summary>
/// Eccezione tipizzata per violazioni delle regole di business della fattura.
/// NON deve essere loggata: è un flusso previsto (Regola 6, CLAUDE.md).
/// </summary>
public sealed class FatturaInvalidaException : Exception
{
    public FatturaMotivoInvalido Motivo { get; }

    public FatturaInvalidaException(FatturaMotivoInvalido motivo, string message)
        : base(message)
    {
        Motivo = motivo;
    }
}
