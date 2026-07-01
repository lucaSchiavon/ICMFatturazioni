namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Motivo per cui una <see cref="AvvisoFatturaInvalidaException"/> è stata lanciata.
/// L'insieme cresce con le validazioni del Manager (fase successiva); i primi
/// motivi nascono dalle guardie di correttezza dell'emissione atomica.
/// </summary>
public enum AvvisoFatturaMotivoInvalido
{
    /// <summary>
    /// Una delle scadenze selezionate è già stata consumata da un altro avviso
    /// (violazione dell'indice univoco <c>UQ_AvvisoFatturaRighe_IdScadenza</c>).
    /// Guardia anti doppia-fatturazione sotto race condition.
    /// </summary>
    ScadenzaGiaInAvviso,

    /// <summary>Nessuna rata selezionata: un avviso deve fatturare almeno una scadenza.</summary>
    NessunaScadenzaSelezionata,

    /// <summary>La data dell'avviso non è stata valorizzata.</summary>
    DataObbligatoria,

    /// <summary>Il cliente (anagrafica) indicato non esiste.</summary>
    AnagraficaNonTrovata,

    /// <summary>
    /// Una rata selezionata non è (più) fatturabile: non esiste, è disattivata,
    /// appartiene a un'altra attività o è già stata consumata. Ricaricare l'elenco.
    /// </summary>
    ScadenzaNonFatturabile,
}

/// <summary>
/// Eccezione tipizzata per violazioni delle regole di business dell'avviso di fattura.
/// NON deve essere loggata: è un flusso previsto (Regola 6, CLAUDE.md).
/// </summary>
public sealed class AvvisoFatturaInvalidaException : Exception
{
    public AvvisoFatturaMotivoInvalido Motivo { get; }

    public AvvisoFatturaInvalidaException(AvvisoFatturaMotivoInvalido motivo, string message)
        : base(message)
    {
        Motivo = motivo;
    }
}
