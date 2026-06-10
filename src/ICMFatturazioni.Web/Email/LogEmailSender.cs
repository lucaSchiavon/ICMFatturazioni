namespace ICMFatturazioni.Web.Email;

/// <summary>
/// Implementazione di SVILUPPO: non invia nulla, scrive l'email (incluso il
/// link) nel log. Permette di provare l'intero flusso attivazione/reset in
/// locale senza credenziali SMTP. Selezionata in Program.cs quando
/// <c>Smtp:Host</c> non è configurato. NON usare in produzione: i link
/// finirebbero nei log.
/// </summary>
public sealed class LogEmailSender : IEmailSender
{
    private readonly ILogger<LogEmailSender> _logger;

    public LogEmailSender(ILogger<LogEmailSender> logger) => _logger = logger;

    public Task SendAsync(string toAddress, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "EMAIL NON INVIATA (LogEmailSender di sviluppo). Configurare 'Smtp:Host' per l'invio reale.\n" +
            "  A:       {To}\n" +
            "  Oggetto: {Subject}\n" +
            "  Corpo:\n{Body}",
            toAddress, subject, htmlBody);
        return Task.CompletedTask;
    }
}
