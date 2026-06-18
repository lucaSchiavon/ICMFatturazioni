namespace ICMFatturazioni.Web.Managers;

public enum DescrizioneAttivitaInvalidoMotivo
{
    DescrizioneObbligatoria,
    DescrizioneDuplicata,
    OrdineNonValido,
}

/// <summary>
/// Eccezione tipizzata per dati non validi di una descrizione attività standard.
/// NON va loggata (flusso previsto, Regola 6).
/// </summary>
public sealed class DescrizioneAttivitaInvalidaException(
    DescrizioneAttivitaInvalidoMotivo motivo,
    string messaggio,
    Exception? innerException = null)
    : Exception(messaggio, innerException)
{
    public DescrizioneAttivitaInvalidoMotivo Motivo { get; } = motivo;
}
