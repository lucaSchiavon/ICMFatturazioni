namespace ICMFatturazioni.Web.Managers;

/// <summary>Motivi possibili di invalidità di una riga <see cref="Entities.AttivitaConsulente"/>.</summary>
public enum AttivitaConsulenteInvalidaMotivo
{
    AttivitaObbligatoria,
    ConsulenteObbligatorio,
    TipoObbligatorio,
    ImportoNonPositivo,

    /// <summary>In modifica: l'importo non può scendere sotto il già pagato (residuo mai negativo, D-C3).</summary>
    ImportoInferiorePagato,

    /// <summary>In modifica: una riga con pagamenti non può passare "a carico del Cliente"
    /// (i pagamenti esistono solo per il carico Studio, dispensa cap. 4-5).</summary>
    CaricoConPagamenti,
}

/// <summary>
/// Eccezione tipizzata sollevata dal manager quando i dati di una riga consulenza
/// non superano la validazione. NON va loggata (è flusso previsto, Regola 6).
/// </summary>
public sealed class AttivitaConsulenteInvalidaException(
    AttivitaConsulenteInvalidaMotivo motivo,
    string messaggio,
    Exception? innerException = null)
    : Exception(messaggio, innerException)
{
    public AttivitaConsulenteInvalidaMotivo Motivo { get; } = motivo;
}

/// <summary>
/// Eccezione sollevata quando si tenta di eliminare una riga consulenza che ha
/// tranche di pagamento attive (decisione D-C2: eliminazione BLOCCATA).
/// </summary>
public sealed class AttivitaConsulenteConPagamentiException(Guid idAttivitaConsulente)
    : Exception($"La consulenza {idAttivitaConsulente} ha pagamenti registrati e non può essere eliminata.")
{
    public Guid IdAttivitaConsulente { get; } = idAttivitaConsulente;
}
