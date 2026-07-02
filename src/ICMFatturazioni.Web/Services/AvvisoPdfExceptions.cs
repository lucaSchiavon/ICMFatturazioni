namespace ICMFatturazioni.Web.Services;

/// <summary>
/// L'avviso richiesto per la stampa non esiste o è stato annullato (mappa a 404).
/// </summary>
public sealed class AvvisoPdfNonTrovatoException : Exception
{
    public AvvisoPdfNonTrovatoException(Guid idAvviso)
        : base($"Avviso di fattura {idAvviso} non trovato o annullato.") { }
}

/// <summary>
/// Mancano dati indispensabili per comporre il documento (azienda emittente non
/// configurata o cliente non trovato). Mappa a 500: è un'incoerenza di dati, non
/// un input utente errato.
/// </summary>
public sealed class AvvisoPdfDatiMancantiException : Exception
{
    public AvvisoPdfDatiMancantiException(string message) : base(message) { }
}
