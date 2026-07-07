namespace ICMFatturazioni.Web.Email;

/// <summary>
/// Invio di una singola email HTML. Astrazione minima con tre implementazioni:
/// <see cref="GraphEmailSender"/> (Microsoft Graph, canale di produzione ISO 27001),
/// <see cref="SmtpEmailSender"/> (SMTP via MailKit, fallback dev/demo) e
/// <see cref="LogEmailSender"/> (sviluppo: link nel log, nessun invio reale).
/// La selezione del provider è in Program.cs (chiave <c>Email:Provider</c>).
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// <paramref name="attachments"/> opzionale: allegati binari (es. il PDF di
    /// cortesia di un avviso/fattura). Null/vuoto = nessun allegato.
    /// </summary>
    Task SendAsync(
        string toAddress,
        string subject,
        string htmlBody,
        IReadOnlyList<EmailAttachment>? attachments = null,
        CancellationToken cancellationToken = default);
}
