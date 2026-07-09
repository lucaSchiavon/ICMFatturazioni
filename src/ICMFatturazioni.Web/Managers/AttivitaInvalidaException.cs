namespace ICMFatturazioni.Web.Managers;

/// <summary>Motivi per cui un'attività non supera la validazione.</summary>
public enum AttivitaInvalidoMotivo
{
    NumeroNonValido,          // Numero ≤ 0 o non fornito
    DescrizioneObbligatoria,
    AnagraficaObbligatoria,
    TipoAttivitaObbligatorio,
    DateIncoerenti,           // violazione ProgettoDefinitivo ≤ ConcessioneEdilizia ≤ InizioLavori
    CodiceDuplicato,          // esiste già un'attività attiva con lo stesso codice per il cliente
}

/// <summary>Eccezione di validazione per <see cref="Entities.Attivita"/> (flusso previsto — non va loggata).</summary>
public sealed class AttivitaInvalidaException : Exception
{
    public AttivitaInvalidaException(AttivitaInvalidoMotivo motivo, string message)
        : base(message) => Motivo = motivo;

    /// <summary>
    /// Overload che preserva l'eccezione originale (es. la <c>SqlException</c> di
    /// violazione dell'indice univoco) come inner, senza esporla all'utente.
    /// </summary>
    public AttivitaInvalidaException(AttivitaInvalidoMotivo motivo, string message, Exception innerException)
        : base(message, innerException) => Motivo = motivo;

    public AttivitaInvalidoMotivo Motivo { get; }
}

/// <summary>
/// Tentativo di eliminare un'attività che ha ancora dettagli attivi collegati.
/// </summary>
public sealed class AttivitaConDipendenzeException(Guid idAttivita)
    : Exception($"L'attività {idAttivita} non può essere eliminata: ha dettagli attivi.")
{
    public Guid IdAttivita { get; } = idAttivita;
}
