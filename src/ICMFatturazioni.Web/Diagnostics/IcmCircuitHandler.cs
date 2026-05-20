using ICMFatturazioni.Web.Entities;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace ICMFatturazioni.Web.Diagnostics;

/// <summary>
/// <see cref="CircuitHandler"/> custom: registra l'apertura/chiusura dei
/// circuit Blazor e — soprattutto — sottoscrive
/// <c>TaskScheduler.UnobservedTaskException</c> per intercettare task
/// asincrone "orfane" che altrimenti perderebbero stack trace.
/// </summary>
/// <remarks>
/// In Blazor Server le eccezioni dei componenti vengono normalmente
/// catturate da <c>ErrorBoundary</c>; questo handler copre il segmento
/// "fuori componente": timer, eventi async, BackgroundService che
/// raggiungono lo scheduler senza catch.
/// </remarks>
internal sealed class IcmCircuitHandler : CircuitHandler, IDisposable
{
    private readonly IErrorLogger _errorLogger;
    private bool _subscribed;

    public IcmCircuitHandler(IErrorLogger errorLogger)
    {
        _errorLogger = errorLogger;
    }

    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        if (!_subscribed)
        {
            // Sottoscrizione al volo: registriamo l'handler globale solo
            // se almeno un circuit è stato aperto. Evita di iscriversi
            // anticipatamente in scenari di test e BackgroundService.
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            _subscribed = true;
        }
        return Task.CompletedTask;
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        // Fire-and-forget volutamente: vogliamo loggare senza bloccare lo
        // scheduler. Il logger ha già il proprio fallback in caso di errore.
        _ = _errorLogger.LogAsync(
            e.Exception,
            contesto: "TaskScheduler.UnobservedTaskException",
            severity: Severity.Critical,
            handled: false);

        // Marchiamo l'eccezione come "osservata" per evitare la kill del
        // processo (comportamento .NET 4.x retained per safety).
        e.SetObserved();
    }

    public void Dispose()
    {
        if (_subscribed)
        {
            TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
            _subscribed = false;
        }
    }
}
