using Microsoft.Extensions.Logging;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// <see cref="ILogger{T}"/> di test che cattura livello e messaggio formattato
/// di ogni voce: serve a verificare ad es. che la sentinella sulla dimensione
/// del DB emetta un Warning solo oltre soglia.
/// </summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    public sealed record Voce(LogLevel Livello, string Messaggio);

    public List<Voce> Voci { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
        => Voci.Add(new Voce(logLevel, formatter(state, exception)));
}
