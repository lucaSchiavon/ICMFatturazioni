namespace ICMFatturazioni.Web.Managers;

public enum TipoDettaglioAttivitaInvalidoMotivo
{
    DescrizioneObbligatoria,
    DescrizioneDuplicata,
}

/// <summary>
/// Eccezione tipizzata per dati non validi di un tipo dettaglio attività.
/// NON va loggata (flusso previsto, Regola 6).
/// </summary>
public sealed class TipoDettaglioAttivitaInvalidaException(
    TipoDettaglioAttivitaInvalidoMotivo motivo,
    string messaggio,
    Exception? innerException = null)
    : Exception(messaggio, innerException)
{
    public TipoDettaglioAttivitaInvalidoMotivo Motivo { get; } = motivo;
}

public sealed class TipoDettaglioAttivitaConDipendenzeException(Guid idTipoDettaglioAttivita)
    : Exception($"Il tipo dettaglio attività {idTipoDettaglioAttivita} è ancora usato da uno o più dettagli attività.")
{
    public Guid IdTipoDettaglioAttivita { get; } = idTipoDettaglioAttivita;
}
