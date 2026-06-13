namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Motivo per cui un'operazione su <c>TipoPagamento</c> è stata rifiutata.
/// L'ordine segue la UX (CLAUDE.md "Ordine dei controlli in eccezioni
/// tipizzate è UX").
/// </summary>
public enum TipoPagamentoInvalidoMotivo
{
    /// <summary>La descrizione è vuota o solo whitespace.</summary>
    DescrizioneObbligatoria,

    /// <summary>Esiste già un tipo di pagamento attivo con la stessa descrizione.</summary>
    DescrizioneDuplicata,

    /// <summary>Esiste già un tipo di pagamento attivo con la stessa sigla.</summary>
    SiglaDuplicata,
}

/// <summary>
/// Eccezione tipizzata sollevata quando un'operazione di Insert/Update su
/// <c>TipoPagamento</c> fallisce per dati non validi. Flusso previsto di
/// validazione: <b>non</b> va loggata in <c>fatt.Log</c> (Regola 6).
/// </summary>
public sealed class TipoPagamentoInvalidaException : Exception
{
    public TipoPagamentoInvalidoMotivo Motivo { get; }

    public TipoPagamentoInvalidaException(TipoPagamentoInvalidoMotivo motivo, string message)
        : base(message)
    {
        Motivo = motivo;
    }

    public TipoPagamentoInvalidaException(TipoPagamentoInvalidoMotivo motivo, string message, Exception inner)
        : base(message, inner)
    {
        Motivo = motivo;
    }
}

/// <summary>
/// Sollevata quando si tenta di eliminare (disattivare) un tipo di pagamento
/// ancora referenziato da codici di pagamento attivi (figli). Pattern "doppia
/// difesa": la UI nasconde il pulsante, il manager rilancia comunque.
/// </summary>
public sealed class TipoPagamentoConDipendenzeException : Exception
{
    public Guid IdTipoPagamento { get; }

    public TipoPagamentoConDipendenzeException(Guid idTipoPagamento)
        : base($"Il tipo di pagamento {idTipoPagamento} non può essere eliminato perché è usato da uno o più codici di pagamento.")
    {
        IdTipoPagamento = idTipoPagamento;
    }
}
