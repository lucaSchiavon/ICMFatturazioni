using Azure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Graph.Users.Item.SendMail;

namespace ICMFatturazioni.Web.Email;

/// <summary>
/// Invio email via Microsoft Graph (sendMail per conto della casella autorizzata
/// dalla Application Access Policy, di default <c>noreply@icmsolutions.it</c>).
/// Canale richiesto dalla compliance ISO 27001: l'invio rientra nel perimetro
/// Entra ID del cliente, senza credenziali SMTP di casella verso un relay esterno.
/// Mirror di ICMVerbali.
/// </summary>
/// <remarks>
/// Il <see cref="GraphServiceClient"/> (con <c>ClientSecretCredential</c>, rinnovo
/// token automatico) è registrato come singleton in Program.cs: qui si inietta e si riusa.
/// </remarks>
public sealed class GraphEmailSender : IEmailSender
{
    private readonly GraphServiceClient _graphClient;
    private readonly GraphOptions _options;
    private readonly ILogger<GraphEmailSender> _logger;

    public GraphEmailSender(
        GraphServiceClient graphClient,
        IOptions<GraphOptions> options,
        ILogger<GraphEmailSender> logger)
    {
        _graphClient = graphClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(
        string toAddress,
        string subject,
        string htmlBody,
        IReadOnlyList<EmailAttachment>? attachments = null,
        CancellationToken cancellationToken = default)
    {
        var sender = _options.SenderAddress;
        if (string.IsNullOrWhiteSpace(sender))
            throw new InvalidOperationException("Graph non configurato: 'Graph:SenderAddress' mancante.");

        var message = new Message
        {
            Subject = subject,
            Body = new ItemBody { ContentType = BodyType.Html, Content = htmlBody },
            ToRecipients =
            [
                new Recipient { EmailAddress = new EmailAddress { Address = toAddress } }
            ],
        };

        if (attachments is not null && attachments.Count > 0)
        {
            message.Attachments = attachments
                .Select(att => (Attachment)new FileAttachment
                {
                    OdataType = "#microsoft.graph.fileAttachment",
                    Name = att.FileName,
                    ContentType = att.ContentType,
                    ContentBytes = att.Content,
                })
                .ToList();
        }

        var requestBody = new SendMailPostRequestBody
        {
            Message = message,
            // false: non salva in "Posta inviata" della casella di servizio (casella
            // noreply, nessuna necessità di archiviazione lato mittente).
            SaveToSentItems = false,
        };

        // Invio "per conto di" (application permission Mail.Send): la casella deve
        // essere quella autorizzata dalla Application Access Policy, altrimenti
        // Exchange Online restituisce accesso negato.
        //
        // Traduzione degli errori tipici in messaggi comprensibili per il log
        // diagnostico (il chiamante li registra via LogErroreAsync). InnerException
        // conserva sempre l'eccezione tecnica originale.
        try
        {
            await _graphClient.Users[sender].SendMail.PostAsync(requestBody, cancellationToken: cancellationToken);
        }
        catch (AuthenticationFailedException ex)
        {
            // Il caso più probabile in produzione: Client Secret scaduto (i secret
            // Entra ID hanno una scadenza) oppure Tenant/Client/Secret errati.
            throw new EmailSendException(
                "Autenticazione a Microsoft Graph negata da Entra ID: Client Secret probabilmente " +
                "SCADUTO o non valido (app ICMWEBAPP). Rigenerare il secret in Entra ID e aggiornare " +
                "la variabile d'ambiente Graph__ClientSecret (produzione) o gli user-secrets (sviluppo). " +
                $"L'email NON è stata inviata (oggetto: {subject}).",
                ex);
        }
        catch (ODataError ex)
        {
            var code = ex.Error?.Code;
            var msg = ex.Error?.Message;
            if (ex.ResponseStatusCode == 403)
                throw new EmailSendException(
                    $"Invio via Microsoft Graph negato (403): il mittente '{sender}' non è autorizzato " +
                    "dalla Application Access Policy di Exchange Online. Verificare che SenderAddress sia la " +
                    $"casella consentita. L'email NON è stata inviata. Dettaglio: {code} - {msg}",
                    ex);
            throw new EmailSendException(
                $"Invio via Microsoft Graph fallito (HTTP {ex.ResponseStatusCode}, oggetto: {subject}). " +
                $"L'email NON è stata inviata. Dettaglio: {code} - {msg}",
                ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Rete, timeout, errori imprevisti: non ingoiare, arricchire e rilanciare.
            throw new EmailSendException(
                $"Invio via Microsoft Graph fallito (oggetto: {subject}). L'email NON è stata recapitata. " +
                "Causa nel dettaglio dell'eccezione.",
                ex);
        }

        // Log "povero" di privacy, coerente con SmtpEmailSender: nessun indirizzo
        // completo o contenuto del messaggio nei log di produzione.
        _logger.LogInformation(
            "Email inviata via Microsoft Graph (oggetto: {Subject}, allegati: {Allegati}).",
            subject, attachments?.Count ?? 0);
    }
}
