using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Diagnostics;

/// <summary>
/// Canale univoco di logging degli errori applicativi (Regola 6 di
/// CLAUDE.md). Iniettato come <b>singleton</b> in ogni layer (Manager,
/// Repository, componenti Blazor, middleware). I componenti UI non
/// chiamano mai <see cref="IErrorLogRepository"/> direttamente.
/// </summary>
public interface IErrorLogger
{
    /// <summary>
    /// Persiste un'eccezione su <c>fatt.LogErrors</c>. Se la scrittura su
    /// DB fallisce per qualunque motivo, esegue fallback su file locale
    /// (<c>logs/error-logger-fallback.log</c>) prima di tornare al chiamante.
    /// <b>Non rilancia mai</b> eccezioni.
    /// </summary>
    /// <param name="ex">Eccezione catturata. Non può essere null.</param>
    /// <param name="contesto">
    /// Componente/metodo che ha catturato l'errore
    /// (es. <c>UtenteManager.AutenticaAsync</c>).
    /// </param>
    /// <param name="descrizioneEstesa">
    /// Descrizione user-friendly o aggiuntiva (es. parametri di input
    /// rilevanti). Mai includere segreti.
    /// </param>
    /// <param name="severity">Default <see cref="Severity.Error"/>.</param>
    /// <param name="handled">
    /// <c>true</c> se l'eccezione è stata gestita senza rilancio (es.
    /// fallback su valore safe nel manager); <c>false</c> se sale lungo
    /// lo stack.
    /// </param>
    Task LogAsync(
        Exception ex,
        string? contesto = null,
        string? descrizioneEstesa = null,
        Severity severity = Severity.Error,
        bool handled = false,
        CancellationToken cancellationToken = default);
}
