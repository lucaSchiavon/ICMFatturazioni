namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Motivo per cui un'operazione su una banca di appoggio è stata rifiutata.
/// L'ordine di enumerazione segue la regola in CLAUDE.md ("Ordine dei controlli
/// in eccezioni tipizzate è UX"): il valore mostrato all'utente è il primo che
/// si verifica nella sequenza di controlli del manager.
/// </summary>
public enum BancaAppoggioInvalidaMotivo
{
    /// <summary>Il nome dell'istituto (Banca) è vuoto o solo whitespace.</summary>
    BancaObbligatoria,

    /// <summary>È stato indicato un CAB ma senza il nome dell'agenzia a cui appartiene.</summary>
    CabSenzaAgenzia,

    /// <summary>L'ABI non è nel formato corretto (5 cifre).</summary>
    AbiNonValido,

    /// <summary>Il CAB non è nel formato corretto (5 cifre).</summary>
    CabNonValido,

    /// <summary>L'IBAN non è formalmente valido (formato o checksum errato).</summary>
    IbanNonValido,

    /// <summary>
    /// L'IBAN è valido ma l'ABI/CAB in esso contenuti non coincidono con quelli
    /// indicati (incoerenza tra IBAN e codici).
    /// </summary>
    IbanIncoerente,

    /// <summary>
    /// Lo stesso intestatario ha già un appoggio attivo con la stessa banca e
    /// agenzia (legame duplicato).
    /// </summary>
    LegameDuplicato,

    /// <summary>
    /// Il cliente indicato non corrisponde a nessun record di
    /// <c>fatt.Anagrafica</c> (intercettato via FK constraint). Una banca senza
    /// cliente è invece legittima: è una banca azienda.
    /// </summary>
    ClienteInesistente,
}

/// <summary>
/// Eccezione tipizzata sollevata quando un'operazione di salvataggio di una
/// banca di appoggio fallisce per dati non validi. È flusso previsto di
/// validazione: <b>non</b> va loggata in <c>fatt.Log</c> (Regola 6).
/// </summary>
public sealed class BancaAppoggioInvalidaException : Exception
{
    public BancaAppoggioInvalidaMotivo Motivo { get; }

    public BancaAppoggioInvalidaException(BancaAppoggioInvalidaMotivo motivo, string message)
        : base(message)
    {
        Motivo = motivo;
    }

    public BancaAppoggioInvalidaException(BancaAppoggioInvalidaMotivo motivo, string message, Exception inner)
        : base(message, inner)
    {
        Motivo = motivo;
    }
}

/// <summary>
/// Sollevata quando si tenta di eliminare (disattivare) una banca di appoggio
/// ancora referenziata da anagrafiche attive
/// (<c>fatt.Anagrafica.IdBancaAppoggio</c>). La UI nasconde il pulsante
/// "Elimina" prima del tentativo, ma il manager rilancia comunque in caso di
/// race condition (pattern "doppia difesa").
/// </summary>
public sealed class BancaAppoggioConDipendenzeException : Exception
{
    public Guid IdBancaAppoggio { get; }

    public BancaAppoggioConDipendenzeException(Guid idBancaAppoggio)
        : base($"La banca {idBancaAppoggio} non può essere eliminata perché è usata da una o più anagrafiche.")
    {
        IdBancaAppoggio = idBancaAppoggio;
    }
}
