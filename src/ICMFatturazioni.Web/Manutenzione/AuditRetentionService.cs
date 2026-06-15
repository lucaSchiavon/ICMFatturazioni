using ICMFatturazioni.Web.Managers.Interfaces;
using Microsoft.Extensions.Options;

namespace ICMFatturazioni.Web.Manutenzione;

/// <summary>
/// <see cref="BackgroundService"/> che, a cadenza configurata, esegue un ciclo di
/// <see cref="IAuditManutenzione"/> (retention temporale + sentinella dimensione).
/// Disabilitabile via <c>AuditRetention:JobAbilitato</c>: a job spento resta solo
/// il pulsante manuale in <c>/admin/audit</c>.
/// </summary>
/// <remarks>
/// La purga è idempotente e gira di rado (default ogni 24 h): manuale e automatica
/// non si scontrano — ripetere "elimina i più vecchi di 36 mesi" non trova nulla
/// la seconda volta. Crea uno scope per ciclo perché <see cref="IAuditManutenzione"/>
/// (e i Manager/Repository che usa) sono Scoped. Ogni eccezione del ciclo è
/// catturata e loggata (Regola 6): un errore non gestito in un BackgroundService
/// sarebbe invisibile.
/// </remarks>
internal sealed class AuditRetentionService : BackgroundService
{
    // Attesa prima del primo ciclo: lascia stabilizzare l'avvio dell'app.
    private static readonly TimeSpan RitardoIniziale = TimeSpan.FromMinutes(2);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AuditRetentionOptions _options;
    private readonly ILogger<AuditRetentionService> _logger;

    public AuditRetentionService(
        IServiceScopeFactory scopeFactory,
        IOptions<AuditRetentionOptions> options,
        ILogger<AuditRetentionService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.JobAbilitato)
        {
            _logger.LogInformation(
                "Job di retention audit disabilitato (AuditRetention:JobAbilitato = false). " +
                "Resta attiva solo la purga manuale in /admin/audit.");
            return;
        }

        // Intervallo difensivo: un valore <= 0 in config non deve creare un loop stretto.
        var intervallo = _options.IntervalloOreJob > 0
            ? TimeSpan.FromHours(_options.IntervalloOreJob)
            : TimeSpan.FromHours(24);

        try
        {
            await Task.Delay(RitardoIniziale, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await EseguiUnCicloAsync(stoppingToken);
                await Task.Delay(intervallo, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Spegnimento ordinato dell'host: nessun errore da segnalare.
        }
    }

    private async Task EseguiUnCicloAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        try
        {
            var manutenzione = scope.ServiceProvider.GetRequiredService<IAuditManutenzione>();
            await manutenzione.EseguiAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // rilancia per uscire dal loop in modo pulito
        }
        catch (Exception ex)
        {
            // Best-effort: un fallimento del ciclo (es. DB irraggiungibile) non
            // deve abbattere il servizio. Lo registriamo via ILogManager (path
            // ricco) e si riprova al ciclo successivo.
            var logManager = scope.ServiceProvider.GetRequiredService<ILogManager>();
            await logManager.LogErroreAsync(ex,
                "Ciclo di manutenzione audit (retention + sentinella dimensione) fallito. " +
                "Verrà ritentato al prossimo intervallo schedulato.",
                "AuditRetentionService.EseguiUnCicloAsync",
                cancellationToken: cancellationToken);
        }
    }
}
