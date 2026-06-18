namespace ICMFatturazioni.Web.Managers;

/// <summary>Motivi per cui un'attività non supera la validazione.</summary>
public enum AttivitaInvalidoMotivo
{
    NumeroNonValido,          // Numero ≤ 0 o non fornito
    DescrizioneObbligatoria,
    AnagraficaObbligatoria,
    TipoAttivitaObbligatorio,
    DateIncoerenti,           // violazione ProgettoDefinitivo ≤ ConcessioneEdilizia ≤ InizioLavori
}

/// <summary>Eccezione di validazione per <see cref="Entities.Attivita"/> (flusso previsto — non va loggata).</summary>
public sealed class AttivitaInvalidaException(AttivitaInvalidoMotivo motivo, string message)
    : Exception(message)
{
    public AttivitaInvalidoMotivo Motivo { get; } = motivo;
}

/// <summary>
/// Tentativo di eliminare un'attività che ha ancora dettagli attivi collegati.
/// </summary>
public sealed class AttivitaConDipendenzeException(Guid idAttivita)
    : Exception($"L'attività {idAttivita} non può essere eliminata: ha dettagli attivi.")
{
    public Guid IdAttivita { get; } = idAttivita;
}
