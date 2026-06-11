using System.Diagnostics;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Logging;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Models;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Implementazione di <see cref="ILogManager"/> (mirror di ICMVerbali). Scrive
/// in modo sincrono-attendibile su <c>fatt.Log</c> (a differenza del provider
/// automatico, che accoda): il chiamante vuole la garanzia che l'errore
/// gestito sia tracciato. In caso di fallimento ricade sul fallback senza mai
/// rilanciare.
/// </summary>
internal sealed class LogManager : ILogManager
{
    private readonly ILogRepository _repository;
    private readonly ILogFallbackWriter _fallback;
    private readonly TimeProvider _clock;

    public LogManager(ILogRepository repository, ILogFallbackWriter fallback, TimeProvider clock)
    {
        _repository = repository;
        _fallback = fallback;
        _clock = clock;
    }

    public async Task LogErroreAsync(
        Exception eccezione,
        string spiegazione,
        string sorgente,
        Guid? utenteId = null,
        Guid? entityId = null,
        string? entityType = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eccezione);

        var entry = new Log
        {
            Id = Guid.CreateVersion7(),
            TimestampUtc = _clock.GetUtcNow().UtcDateTime,
            Livello = LogLivello.Error,
            Sorgente = sorgente,
            // Sanitizziamo messaggio, stack e spiegazione: possono contenere
            // connection string/token (Regola 6 di CLAUDE.md).
            Messaggio = LogSanitizer.Sanitize(eccezione.Message) ?? string.Empty,
            EccezioneTipo = eccezione.GetType().FullName,
            StackTrace = LogSanitizer.Sanitize(eccezione.ToString()),
            SpiegazioneUtente = LogSanitizer.Sanitize(spiegazione),
            RequestId = Activity.Current?.Id,
            UtenteId = utenteId,
            EntityId = entityId,
            EntityType = entityType,
        };

        try
        {
            await _repository.InsertAsync(entry, cancellationToken);
        }
        catch (Exception scritturaFallita)
        {
            // Un logger non deve mai lanciare (maschererebbe l'errore originale):
            // ricadiamo sui sink di fallback e proseguiamo.
            _fallback.Write(entry, scritturaFallita);
        }
    }

    public Task<LogRisultato> CercaAsync(LogFiltro filtro, CancellationToken cancellationToken = default)
        => _repository.CercaAsync(filtro, cancellationToken);

    public Task<int> PurgaPrecedentiAsync(int giorni, CancellationToken cancellationToken = default)
    {
        // Soglia calcolata col TimeProvider (testabile). |giorni| per tollerare
        // un input negativo accidentale dalla UI senza purgare il futuro.
        var soglia = _clock.GetUtcNow().UtcDateTime.AddDays(-Math.Abs(giorni));
        return _repository.PurgaPrecedentiAsync(soglia, cancellationToken);
    }
}
