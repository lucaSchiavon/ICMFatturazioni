namespace ICMFatturazioni.Web.Services;

/// <summary>
/// La fattura richiesta per la stampa non esiste o è stata annullata (mappa a 404).
/// </summary>
public sealed class FatturaPdfNonTrovatoException : Exception
{
    public FatturaPdfNonTrovatoException(Guid idFattura)
        : base($"Fattura {idFattura} non trovata o annullata.") { }
}
