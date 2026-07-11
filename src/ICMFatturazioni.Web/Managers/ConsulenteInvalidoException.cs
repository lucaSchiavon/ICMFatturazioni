namespace ICMFatturazioni.Web.Managers;

/// <summary>Motivi possibili di invalidità di un <see cref="Entities.Consulente"/>.</summary>
public enum ConsulenteInvalidoMotivo
{
    DescrizioneObbligatoria,
    DescrizioneDuplicata,
}

/// <summary>
/// Eccezione tipizzata sollevata dal manager quando i dati di un consulente
/// non superano la validazione. NON va loggata (è flusso previsto, Regola 6).
/// </summary>
public sealed class ConsulenteInvalidoException(
    ConsulenteInvalidoMotivo motivo,
    string messaggio,
    Exception? innerException = null)
    : Exception(messaggio, innerException)
{
    public ConsulenteInvalidoMotivo Motivo { get; } = motivo;
}

/// <summary>
/// Eccezione sollevata quando si tenta di disattivare un consulente
/// ancora referenziato da una o più righe consulenza.
/// </summary>
public sealed class ConsulenteConDipendenzeException(Guid idConsulente)
    : Exception($"Il consulente {idConsulente} è ancora usato da una o più attività consulenti.")
{
    public Guid IdConsulente { get; } = idConsulente;
}
