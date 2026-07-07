namespace ICMFatturazioni.Web.Email;

/// <summary>
/// Implementazione di SVILUPPO: non invia nulla, scrive l'email (incluso il
/// link) nel log. Permette di provare l'intero flusso attivazione/reset in
/// locale senza credenziali Graph/SMTP. Selezionata in Program.cs quando nessun
/// provider reale è configurato (o con <c>Email:Provider=Log</c> esplicito).
/// NON usare in produzione: i link finirebbero nei log.
/// </summary>
public sealed class LogEmailSender : IEmailSender
{
    private readonly ILogger<LogEmailSender> _logger;

    public LogEmailSender(ILogger<LogEmailSender> logger) => _logger = logger;

    public Task SendAsync(
        string toAddress,
        string subject,
        string htmlBody,
        IReadOnlyList<EmailAttachment>? attachments = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "EMAIL NON INVIATA (LogEmailSender di sviluppo). Configurare 'Graph' o 'Smtp:Host' per l'invio reale.\n" +
            "  A:        {To}\n" +
            "  Oggetto:  {Subject}\n" +
            "  Allegati: {Allegati}\n" +
            "  Corpo:\n{Body}",
            toAddress, subject, attachments?.Count ?? 0, htmlBody);
        return Task.CompletedTask;
    }
}
