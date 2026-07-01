namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Motivo per cui un <see cref="ScadenzaPagamentoInvalidaException"/> è stata lanciata.
/// </summary>
public enum ScadenzaPagamentoMotivoInvalido
{
    /// <summary>La riga dettaglio parent è collegata a una fattura; le scadenze non possono essere modificate.</summary>
    DettaglioFatturato,

    /// <summary>La rata è già evasa da un avviso di fattura: è congelata finché l'avviso non viene annullato.</summary>
    RataEvasa,

    /// <summary>La data di scadenza non è stata valorizzata.</summary>
    DataObbligatoria,

    /// <summary>L'importo è ≤ 0.</summary>
    ImportoNonValido,

    /// <summary>La somma delle scadenze attive eccederebbe l'importo del dettaglio attività.</summary>
    SommaEccedeImporto,
}

/// <summary>
/// Eccezione tipizzata per violazioni delle regole di business di <c>ScadenzaPagamento</c>.
/// NON deve essere loggata: è un flusso previsto (Regola 6, CLAUDE.md).
/// </summary>
public sealed class ScadenzaPagamentoInvalidaException : Exception
{
    public ScadenzaPagamentoMotivoInvalido Motivo { get; }

    public ScadenzaPagamentoInvalidaException(ScadenzaPagamentoMotivoInvalido motivo, string message)
        : base(message)
    {
        Motivo = motivo;
    }
}
