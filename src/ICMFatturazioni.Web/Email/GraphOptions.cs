namespace ICMFatturazioni.Web.Email;

/// <summary>
/// Configurazione per l'invio email via Microsoft Graph (OAuth 2.0 Client
/// Credentials Flow, App Registration "ICMWEBAPP" su Entra ID). Bindata dalla
/// sezione <c>Graph</c> di appsettings.json. È il canale richiesto dalla
/// compliance ISO 27001 (vedi <c>InvioPostaElettronica.pdf</c> in ICMVerbali):
/// in produzione ISO il provider selezionato deve essere Graph, non SMTP.
/// Mirror di ICMVerbali.
/// </summary>
/// <remarks>
/// SEGRETI: <see cref="ClientSecret"/> NON va MAI in appsettings versionato.
/// In sviluppo si usa user-secrets (<c>Graph:ClientSecret</c>); in produzione
/// (VM on-premise) la variabile d'ambiente di sistema <c>Graph__ClientSecret</c>,
/// leggibile solo dall'account App Pool IIS. Vedi CLAUDE.md "Gestione di segreti
/// e configurazioni".
///
/// <see cref="SenderAddress"/> è vincolato lato Exchange Online da una
/// Application Access Policy: l'app può inviare esclusivamente per conto di
/// <c>noreply@icmsolutions.it</c>. Qualsiasi altra casella restituisce accesso negato.
/// </remarks>
public sealed class GraphOptions
{
    public const string SectionName = "Graph";

    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }

    /// <summary>Casella mittente autorizzata dalla Application Access Policy.</summary>
    public string SenderAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "ICM Fatturazioni";

    /// <summary>
    /// Configurato solo se tutti i parametri di autenticazione + mittente sono
    /// presenti. Usato in Program.cs per la selezione del provider in modalità Auto.
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(TenantId)
        && !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret)
        && !string.IsNullOrWhiteSpace(SenderAddress);
}
