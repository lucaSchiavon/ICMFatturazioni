namespace ICMFatturazioni.Web.Managers;

/// <summary>Motivo per cui una <see cref="AliquotaInvalidaException"/> è stata lanciata.</summary>
public enum AliquotaMotivoInvalido
{
    /// <summary>La descrizione è vuota.</summary>
    DescrizioneObbligatoria,

    /// <summary>Il valore è negativo o fuori dall'intervallo ammesso.</summary>
    ValoreNonValido,

    /// <summary>Tentata eliminazione di un'aliquota di sistema (usata dal calcolo).</summary>
    AliquotaDiSistema,
}

/// <summary>
/// Eccezione tipizzata per violazioni delle regole di business di <c>Aliquota</c>.
/// NON deve essere loggata: è un flusso previsto (Regola 6, CLAUDE.md).
/// </summary>
public sealed class AliquotaInvalidaException : Exception
{
    public AliquotaMotivoInvalido Motivo { get; }

    public AliquotaInvalidaException(AliquotaMotivoInvalido motivo, string message)
        : base(message)
    {
        Motivo = motivo;
    }
}
