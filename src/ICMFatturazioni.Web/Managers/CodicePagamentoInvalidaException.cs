namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Motivo per cui un'operazione su <c>CodicePagamento</c> è stata rifiutata.
/// Ordine = UX (CLAUDE.md).
/// </summary>
public enum CodicePagamentoInvalidoMotivo
{
    /// <summary>La descrizione è vuota o solo whitespace.</summary>
    DescrizioneObbligatoria,

    /// <summary>Il tipo di pagamento non è stato selezionato.</summary>
    TipoObbligatorio,

    /// <summary>Il numero di scadenze non è tra 1 e 3.</summary>
    NumScadenzeNonValido,

    /// <summary>
    /// I giorni di scadenza non sono coerenti col numero di rate (servono GG1..N
    /// valorizzati, gli altri nulli).
    /// </summary>
    GiorniScadenzaIncoerenti,

    /// <summary>Sono stati indicati giorni aggiuntivi (GGpiu) senza il fine mese.</summary>
    GiorniAggiuntiviSenzaFineMese,

    /// <summary>Esiste già un codice di pagamento attivo con la stessa descrizione.</summary>
    DescrizioneDuplicata,

    /// <summary>Il tipo di pagamento indicato non esiste (intercettato via FK).</summary>
    TipoInesistente,
}

/// <summary>
/// Eccezione tipizzata di validazione su <c>CodicePagamento</c>. Flusso previsto:
/// <b>non</b> va loggata in <c>fatt.Log</c> (Regola 6).
/// </summary>
public sealed class CodicePagamentoInvalidaException : Exception
{
    public CodicePagamentoInvalidoMotivo Motivo { get; }

    public CodicePagamentoInvalidaException(CodicePagamentoInvalidoMotivo motivo, string message)
        : base(message)
    {
        Motivo = motivo;
    }

    public CodicePagamentoInvalidaException(CodicePagamentoInvalidoMotivo motivo, string message, Exception inner)
        : base(message, inner)
    {
        Motivo = motivo;
    }
}

/// <summary>
/// Sollevata quando si tenta di eliminare (disattivare) un codice di pagamento
/// ancora referenziato da anagrafiche attive (<c>fatt.Anagrafica.IdPag</c>).
/// Pattern "doppia difesa".
/// </summary>
public sealed class CodicePagamentoConDipendenzeException : Exception
{
    public Guid IdCodicePagamento { get; }

    public CodicePagamentoConDipendenzeException(Guid idCodicePagamento)
        : base($"Il codice di pagamento {idCodicePagamento} non può essere eliminato perché è usato da una o più anagrafiche.")
    {
        IdCodicePagamento = idCodicePagamento;
    }
}
