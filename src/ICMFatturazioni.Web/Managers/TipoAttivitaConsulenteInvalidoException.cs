namespace ICMFatturazioni.Web.Managers;

/// <summary>Motivi possibili di invalidità di un <see cref="Entities.TipoAttivitaConsulente"/>.</summary>
public enum TipoAttivitaConsulenteInvalidoMotivo
{
    DescrizioneObbligatoria,
    DescrizioneDuplicata,
}

/// <summary>
/// Eccezione tipizzata sollevata dal manager quando i dati di un tipo attività
/// consulente non superano la validazione. NON va loggata (è flusso previsto, Regola 6).
/// </summary>
public sealed class TipoAttivitaConsulenteInvalidoException(
    TipoAttivitaConsulenteInvalidoMotivo motivo,
    string messaggio,
    Exception? innerException = null)
    : Exception(messaggio, innerException)
{
    public TipoAttivitaConsulenteInvalidoMotivo Motivo { get; } = motivo;
}

/// <summary>
/// Eccezione sollevata quando si tenta di disattivare un tipo attività consulente
/// ancora referenziato da una o più righe consulenza.
/// </summary>
public sealed class TipoAttivitaConsulenteConDipendenzeException(Guid idTipoAttivitaConsulente)
    : Exception($"Il tipo attività consulente {idTipoAttivitaConsulente} è ancora usato da una o più attività consulenti.")
{
    public Guid IdTipoAttivitaConsulente { get; } = idTipoAttivitaConsulente;
}
