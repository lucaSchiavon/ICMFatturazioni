namespace ICMFatturazioni.Web.Managers;

/// <summary>Motivi possibili di invalidità di una tranche <see cref="Entities.AttivitaConsulentePagamento"/>.</summary>
public enum AttivitaConsulentePagamentoInvalidoMotivo
{
    RigaObbligatoria,
    RigaNonTrovata,

    /// <summary>La riga è a carico del Cliente: i pagamenti esistono solo per il carico Studio (dispensa cap. 4-5).</summary>
    RigaNonACaricoStudio,

    ImportoNonPositivo,

    /// <summary>La tranche supera il residuo (D-C3: residuo mai negativo, vale anche in modifica).</summary>
    ImportoOltreResiduo,
}

/// <summary>
/// Eccezione tipizzata sollevata dal manager quando una tranche di pagamento
/// non supera la validazione. NON va loggata (è flusso previsto, Regola 6).
/// </summary>
public sealed class AttivitaConsulentePagamentoInvalidoException(
    AttivitaConsulentePagamentoInvalidoMotivo motivo,
    string messaggio,
    Exception? innerException = null)
    : Exception(messaggio, innerException)
{
    public AttivitaConsulentePagamentoInvalidoMotivo Motivo { get; } = motivo;
}
