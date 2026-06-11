using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Logging;

/// <summary>
/// Canale di ultima istanza per gli eventi di log quando la scrittura su DB
/// (<c>fatt.Log</c>) fallisce — tipicamente perché è proprio il database a
/// essere irraggiungibile, cioè lo scenario che più conta tracciare.
/// </summary>
/// <remarks>
/// Scrive su <b>due</b> sink (scelta utente, vedi Regola 6 di CLAUDE.md):
/// <list type="bullet">
///   <item><c>Console.Error</c> — raccolto automaticamente dalle piattaforme
///   PaaS (Azure App Service Log Stream, container logs);</item>
///   <item>file locale <c>logs/error-logger-fallback.log</c> — utile in
///   hosting on-prem / IIS dove lo stderr di un servizio si perde.</item>
/// </list>
/// Non rilancia <b>mai</b>: un logger che lancia maschererebbe l'errore originale.
/// </remarks>
public interface ILogFallbackWriter
{
    /// <summary>
    /// Riversa una riga di log sui sink di fallback. <paramref name="causa"/> è
    /// l'eccezione che ha impedito la scrittura su DB (facoltativa, a scopo
    /// diagnostico).
    /// </summary>
    void Write(Log entry, Exception? causa = null);
}
