namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Motivo per cui una <see cref="SpesaAnticipataInvalidaException"/> è stata lanciata.
/// </summary>
public enum SpesaAnticipataMotivoInvalido
{
    /// <summary>La data della spesa non è stata valorizzata.</summary>
    DataObbligatoria,

    /// <summary>La descrizione è vuota.</summary>
    DescrizioneObbligatoria,

    /// <summary>L'importo è ≤ 0.</summary>
    ImportoNonValido,
}

/// <summary>
/// Eccezione tipizzata per violazioni delle regole di business di <c>SpesaAnticipata</c>.
/// NON deve essere loggata: è un flusso previsto (Regola 6, CLAUDE.md).
/// </summary>
public sealed class SpesaAnticipataInvalidaException : Exception
{
    public SpesaAnticipataMotivoInvalido Motivo { get; }

    public SpesaAnticipataInvalidaException(SpesaAnticipataMotivoInvalido motivo, string message)
        : base(message)
    {
        Motivo = motivo;
    }
}
