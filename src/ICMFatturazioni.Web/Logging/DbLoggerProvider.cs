using System.Diagnostics;
using ICMFatturazioni.Web.Entities;
using Microsoft.Extensions.Logging;

namespace ICMFatturazioni.Web.Logging;

/// <summary>
/// <see cref="ILoggerProvider"/> che persiste su <c>fatt.Log</c> i log di
/// livello Warning e superiore emessi dalla pipeline standard — incluse le
/// eccezioni non gestite del framework (HTTP e circuiti Blazor), che ASP.NET
/// logga a livello Error. È la "rete automatica": nessuna chiamata esplicita
/// richiesta per tracciare gli errori non gestiti.
/// </summary>
/// <remarks>
/// Anti-ricorsione: le categorie del namespace di logging e del driver SQL
/// ricevono un logger no-op. Senza questo, un errore durante la scrittura del
/// log genererebbe un nuovo log che rientra in coda all'infinito.
/// </remarks>
public sealed class DbLoggerProvider : ILoggerProvider
{
    private static readonly string[] CategorieEscluse =
    [
        "ICMFatturazioni.Web.Logging", // questo provider, la coda, il writer
        "Microsoft.Data.SqlClient",    // il driver usato per scrivere i log
    ];

    private readonly ILogQueue _queue;

    public DbLoggerProvider(ILogQueue queue) => _queue = queue;

    public ILogger CreateLogger(string categoryName)
    {
        foreach (var escluso in CategorieEscluse)
        {
            if (categoryName.StartsWith(escluso, StringComparison.Ordinal))
            {
                return NullLogger.Instance;
            }
        }
        return new DbLogger(categoryName, _queue);
    }

    public void Dispose() { }

    // -----------------------------------------------------------------
    // Logger concreto: trasforma l'evento in una riga di fatt.Log e la accoda.
    // -----------------------------------------------------------------
    private sealed class DbLogger : ILogger
    {
        private readonly string _categoria;
        private readonly ILogQueue _queue;

        public DbLogger(string categoria, ILogQueue queue)
        {
            _categoria = categoria;
            _queue = queue;
        }

        // Persistiamo solo Warning+.
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var entry = new Log
            {
                Id = Guid.CreateVersion7(),
                TimestampUtc = DateTime.UtcNow,
                Livello = (LogLivello)(byte)logLevel,
                Sorgente = _categoria,
                // Sanitizziamo: il messaggio e lo stack di un'eccezione possono
                // contenere connection string, token, ecc. (Regola 6).
                Messaggio = LogSanitizer.Sanitize(formatter(state, exception)) ?? string.Empty,
                EccezioneTipo = exception?.GetType().FullName,
                StackTrace = LogSanitizer.Sanitize(exception?.ToString()),
                SpiegazioneUtente = null, // solo il path esplicito la valorizza
                RequestId = Activity.Current?.Id,
            };

            _queue.TryEnqueue(entry); // non blocca mai
        }
    }

    // No-op per le categorie escluse (anti-ricorsione).
    private sealed class NullLogger : ILogger
    {
        public static readonly NullLogger Instance = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
