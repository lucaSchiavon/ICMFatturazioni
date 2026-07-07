namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Motivo per cui un'operazione su <c>Cantiere</c> è stata rifiutata.
/// L'ordine di enumerazione segue la regola in CLAUDE.md ("Ordine dei
/// controlli in eccezioni tipizzate è UX"): il valore mostrato all'utente
/// è il primo che si verifica nella sequenza di controlli del manager.
/// </summary>
public enum CantiereInvalidoMotivo
{
    /// <summary>Nessuna attività associata (Guid.Empty): un cantiere non può esistere senza attività.</summary>
    AttivitaObbligatoria,

    /// <summary>L'ubicazione è vuota o solo whitespace.</summary>
    UbicazioneObbligatoria,

    /// <summary>L'ubicazione supera i 300 caratteri della colonna.</summary>
    UbicazioneTroppoLunga,

    /// <summary>La tipologia è vuota o solo whitespace.</summary>
    TipologiaObbligatoria,

    /// <summary>La tipologia supera i 500 caratteri della colonna.</summary>
    TipologiaTroppoLunga,

    /// <summary>L'importo appalto è negativo.</summary>
    ImportoNegativo,

    /// <summary>
    /// L'attività indicata non esiste o non è attiva (pre-check manager su
    /// <c>fatt.Attivita</c>; FK_Cantiere_Progetto come sentinel — doppia difesa).
    /// </summary>
    AttivitaInesistente,
}

/// <summary>
/// Eccezione tipizzata sollevata quando un'operazione di Insert/Update
/// su <c>Cantiere</c> fallisce per dati non validi. Flusso previsto di
/// validazione: NON va loggata in <c>fatt.Log</c> (Regola 6).
/// </summary>
public sealed class CantiereInvalidoException : Exception
{
    public CantiereInvalidoMotivo Motivo { get; }

    public CantiereInvalidoException(CantiereInvalidoMotivo motivo, string message)
        : base(message)
    {
        Motivo = motivo;
    }

    public CantiereInvalidoException(CantiereInvalidoMotivo motivo, string message, Exception inner)
        : base(message, inner)
    {
        Motivo = motivo;
    }
}
