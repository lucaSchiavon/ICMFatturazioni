namespace ICMFatturazioni.Web.Email;

/// <summary>
/// Composizione dei corpi email per attivazione/reset account. HTML semplice e
/// autoportante (nessun asset esterno). Il link è sempre mostrato anche come
/// testo, così resta utilizzabile con client che non rendono i bottoni. Il
/// colore istituzionale è l'icm-blue di brand-guidelines.md.
/// </summary>
public static class AccountEmailTemplates
{
    // Colore istituzionale ICM (icm-blue-500), coerente con il brand.
    private const string IcmBlue = "#245F8C";

    public static (string Subject, string HtmlBody) Attivazione(string activationUrl, string username, int oreValidita)
    {
        const string subject = "Attiva il tuo account ICM Fatturazioni";
        var body = Wrap(
            titolo: "Benvenuto in ICM Fatturazioni",
            intro: $"È stato creato un account per <strong>{Esc(username)}</strong>. " +
                   "Per accedere imposta la tua password tramite il pulsante qui sotto.",
            ctaText: "Imposta la password",
            ctaUrl: activationUrl,
            nota: $"Il link è valido per {oreValidita / 24} giorni. " +
                  "Se non hai richiesto questo account, ignora questa email.");
        return (subject, body);
    }

    public static (string Subject, string HtmlBody) Reset(string resetUrl, string username, int oreValidita)
    {
        const string subject = "Reimposta la password ICM Fatturazioni";
        var body = Wrap(
            titolo: "Reimposta la password",
            intro: "Abbiamo ricevuto una richiesta di reimpostazione password per " +
                   $"<strong>{Esc(username)}</strong>. Usa il pulsante qui sotto per sceglierne una nuova.",
            ctaText: "Reimposta la password",
            ctaUrl: resetUrl,
            nota: $"Il link è valido per {oreValidita} ora/e. " +
                  "Se non hai richiesto il reset, ignora questa email: la password attuale resta valida.");
        return (subject, body);
    }

    private static string Wrap(string titolo, string intro, string ctaText, string ctaUrl, string nota)
        => $@"<!DOCTYPE html>
<html lang=""it"">
<body style=""font-family: Inter, Segoe UI, Arial, sans-serif; color: #1a1a1a; line-height: 1.5;"">
  <div style=""max-width: 480px; margin: 0 auto; padding: 24px;"">
    <h2 style=""color: {IcmBlue}; margin-bottom: 8px;"">{Esc(titolo)}</h2>
    <p>{intro}</p>
    <p style=""margin: 28px 0;"">
      <a href=""{Esc(ctaUrl)}""
         style=""background: {IcmBlue}; color: #fff; text-decoration: none;
                padding: 12px 24px; border-radius: 4px; display: inline-block;"">
        {Esc(ctaText)}
      </a>
    </p>
    <p style=""font-size: 13px; color: #555;"">
      Se il pulsante non funziona, copia e incolla questo indirizzo nel browser:<br>
      <a href=""{Esc(ctaUrl)}"">{Esc(ctaUrl)}</a>
    </p>
    <hr style=""border: none; border-top: 1px solid #e0e0e0; margin: 24px 0;"">
    <p style=""font-size: 12px; color: #888;"">{Esc(nota)}</p>
  </div>
</body>
</html>";

    // Escape HTML minimale per i valori interpolati (username, URL).
    private static string Esc(string value) => System.Net.WebUtility.HtmlEncode(value);
}
