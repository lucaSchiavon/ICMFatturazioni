using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ICMFatturazioni.Web.Email;

/// <summary>
/// Invio email via SMTP con MailKit. Pensato per Brevo
/// (smtp-relay.brevo.com:587 STARTTLS) ma funziona con qualunque relay. Una
/// connessione per invio: i volumi (attivazioni/reset) sono bassi, nessun pool.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string toAddress, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(toAddress));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        var socketOptions = _options.UseStartTls
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.Auto;

        var host = _options.Host
            ?? throw new InvalidOperationException("SMTP non configurato: 'Smtp:Host' mancante.");
        await client.ConnectAsync(host, _options.Port, socketOptions, cancellationToken);

        var user = _options.User;
        if (!string.IsNullOrEmpty(user))
        {
            await client.AuthenticateAsync(user, _options.Password ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        // Niente indirizzo completo o contenuto nel log (privacy / no link in
        // chiaro nei log di produzione): solo conferma operativa.
        _logger.LogInformation("Email inviata via SMTP (oggetto: {Subject}).", subject);
    }
}
