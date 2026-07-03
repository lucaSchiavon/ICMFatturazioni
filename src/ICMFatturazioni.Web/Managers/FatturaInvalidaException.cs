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
