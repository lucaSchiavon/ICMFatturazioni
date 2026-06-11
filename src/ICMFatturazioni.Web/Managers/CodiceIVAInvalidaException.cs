namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Motivo per cui un'operazione su <c>CodiceIVA</c> è stata rifiutata.
/// L'ordine di enumerazione segue la regola in CLAUDE.md ("Ordine dei
/// controlli in eccezioni tipizzate è UX"): il valore mostrato all'utente è il
/// primo che si verifica nella sequenza di controlli del manager.
/// </summary>
public enum CodiceIVAInvalidaMotivo
{
    /// <summary>Il codice (sigla) è vuoto o solo whitespace.</summary>
    CodiceObbligatorio,

    /// <summary>La descrizione è vuota o solo whitespace.</summary>
    DescrizioneObbligatoria,

    /// <summary>L'aliquota è negativa.</summary>
    AliquotaNonValida,

    /// <summary>
    /// Aliquota = 0 (non imponibile/esente) ma la Natura non è stata indicata:
    /// la qualificazione AdE è obbligatoria in questo caso (dispensa §6.2).
    /// </summary>
    NaturaObbligatoria,

    /// <summary>
    /// Aliquota &gt; 0 (imponibile) ma è stata indicata una Natura: non è
    /// ammessa, va lasciata vuota.
    /// </summary>
    NaturaNonAmmessa,

    /// <summary>
    /// Aliquota = 0 (non imponibile/esente) ma l'Obbligo bollo non è stato
    /// scelto: per queste operazioni la scelta è obbligatoria e deve cadere su
    /// Sì o No (il "non impostato" non è ammesso).
    /// </summary>
    ObbligoBolloObbligatorio,

    /// <summary>
    /// Esiste già un altro codice IVA attivo con la stessa sigla
    /// (<c>Codice</c> univoco tra gli attivi).
    /// </summary>
    CodiceDuplicato,

    /// <summary>
    /// La Natura indicata non corrisponde a nessun record di
    /// <c>fatt.NatureIVA</c> (intercettata via FK constraint).
    /// </summary>
    NaturaInesistente,
}

/// <summary>
/// Eccezione tipizzata sollevata quando un'operazione di Insert/Update su
/// <c>CodiceIVA</c> fallisce per dati non validi. È flusso previsto di
/// validazione: <b>non</b> va loggata in <c>fatt.Log</c> (Regola 6).
/// </summary>
public sealed class CodiceIVAInvalidaException : Exception
{
    public CodiceIVAInvalidaMotivo Motivo { get; }

    public CodiceIVAInvalidaException(CodiceIVAInvalidaMotivo motivo, string message)
        : base(message)
    {
        Motivo = motivo;
    }

    public CodiceIVAInvalidaException(CodiceIVAInvalidaMotivo motivo, string message, Exception inner)
        : base(message, inner)
    {
        Motivo = motivo;
    }
}

/// <summary>
/// Sollevata quando si tenta di eliminare (disattivare) un codice IVA ancora
/// referenziato da anagrafiche attive. La UI nasconde il pulsante "Elimina"
/// prima del tentativo, ma il manager rilancia comunque in caso di race
/// condition (pattern "doppia difesa").
/// </summary>
public sealed class CodiceIVAConDipendenzeException : Exception
{
    public Guid IdCodiceIVA { get; }

    public CodiceIVAConDipendenzeException(Guid idCodiceIVA)
        : base($"Il codice IVA {idCodiceIVA} non può essere eliminato perché è usato da una o più anagrafiche.")
    {
        IdCodiceIVA = idCodiceIVA;
    }
}
