namespace ICMFatturazioni.Web.Email;

/// <summary>
/// Invio di una singola email HTML. Astrazione minima: l'implementazione reale
/// (<see cref="SmtpEmailSender"/>) usa SMTP via MailKit; in sviluppo, quando
/// l'SMTP non è configurato, si usa <see cref="LogEmailSender"/> (link nel log).
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string toAddress, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
