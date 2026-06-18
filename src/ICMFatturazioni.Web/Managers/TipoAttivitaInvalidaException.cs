namespace ICMFatturazioni.Web.Managers;

/// <summary>Motivi possibili di invalidità di un <see cref="Entities.TipoAttivita"/>.</summary>
public enum TipoAttivitaInvalidoMotivo
{
    DescrizioneObbligatoria,
    DescrizioneDuplicata,
}

/// <summary>
/// Eccezione tipizzata sollevata dal manager quando i dati di un tipo attività
/// non superano la validazione. NON va loggata (è flusso previsto, Regola 6).
/// </summary>
public sealed class TipoAttivitaInvalidaException(
    TipoAttivitaInvalidoMotivo motivo,
    string messaggio,
    Exception? innerException = null)
    : Exception(messaggio, innerException)
{
    public TipoAttivitaInvalidoMotivo Motivo { get; } = motivo;
}

/// <summary>
/// Eccezione sollevata quando si tenta di disattivare un tipo attività
/// ancora referenziato da una o più attività.
/// </summary>
public sealed class TipoAttivitaConDipendenzeException(Guid idTipoAttivita)
    : Exception($"Il tipo attività {idTipoAttivita} è ancora usato da una o più attività.")
{
    public Guid IdTipoAttivita { get; } = idTipoAttivita;
}
