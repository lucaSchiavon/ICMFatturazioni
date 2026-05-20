using ICMFatturazioni.Web.Entities;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace ICMFatturazioni.Web.Diagnostics;

/// <summary>
/// Handler globale registrato con <c>services.AddExceptionHandler&lt;T&gt;()</c>.
/// Cattura tutte le eccezioni non gestite del pipeline HTTP e le inoltra
/// a <see cref="IErrorLogger"/>. Restituisce <c>false</c> per lasciare
/// che l'<c>UseExceptionHandler</c> standard produca la risposta di
/// errore (in produzione la pagina /Error, in sviluppo la dev page).
/// </summary>
internal sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IErrorLogger _logger;

    public GlobalExceptionHandler(IErrorLogger logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // L'eccezione qui è quella che "esce" dal pipeline: la consideriamo
        // sempre non-handled (l'app si è arresa). Severity Critical perché
        // un'eccezione che bubble-up al middleware è quella che butterebbe
        // a terra la response.
        await _logger.LogAsync(
            exception,
            contesto: "ASP.NET pipeline (UseExceptionHandler)",
            descrizioneEstesa: $"Path: {httpContext.Request.Path}{httpContext.Request.QueryString}",
            severity: Severity.Critical,
            handled: false,
            cancellationToken: cancellationToken);

        // Ritornando false lasciamo che la pipeline produca la response
        // di errore standard (pagina /Error in prod, dev page in dev).
        return false;
    }
}
