namespace ICMFatturazioni.Web.Email;

/// <summary>
/// Fallimento nell'invio di un'email. Il messaggio è già formulato in modo
/// comprensibile (causa probabile + azione correttiva), così quando il chiamante
/// lo registra via <c>ILogManager.LogErroreAsync</c> il log di produzione è
/// diagnosticabile senza dover interpretare l'eccezione tecnica sottostante
/// (conservata in <see cref="Exception.InnerException"/>).
/// </summary>
/// <remarks>
/// A differenza delle eccezioni di validazione/flusso atteso, questa è un errore
/// operativo reale (l'email NON è stata recapitata) e VA loggata in
/// <c>fatt.Log</c> (Regola 6 di CLAUDE.md). Mirror di ICMVerbali.
/// </remarks>
public sealed class EmailSendException : Exception
{
    public EmailSendException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
