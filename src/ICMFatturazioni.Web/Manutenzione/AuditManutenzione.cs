using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Repositories.Interfaces;
using Microsoft.Extensions.Options;

namespace ICMFatturazioni.Web.Manutenzione;

/// <summary>
/// Implementazione di <see cref="IAuditManutenzione"/>. Due responsabilità,
/// volutamente distinte (vedi nota tecnica audit-dimensionamento §6):
///   1. RETENTION — elimina l'audit più vecchio di <c>MesiConservazione</c>.
///      È l'unico passo che cancella, e cancella SOLO per età.
///   2. SENTINELLA — legge la dimensione dei file dati del DB e, se supera
///      <c>SogliaAllarmeGb</c>, emette un Warning. NON cancella: il tetto dei
///      10 GB di Express è sul DB intero, quindi qui si AVVISA soltanto e si
///      lascia all'amministratore la decisione.
/// </summary>
internal sealed class AuditManutenzione : IAuditManutenzione
{
    private readonly IAuditManager _auditManager;
    private readonly IDatabaseSizeRepository _sizeRepository;
    private readonly AuditRetentionOptions _options;
    private readonly ILogger<AuditManutenzione> _logger;

    public AuditManutenzione(
        IAuditManager auditManager,
        IDatabaseSizeRepository sizeRepository,
        IOptions<AuditRetentionOptions> options,
        ILogger<AuditManutenzione> logger)
    {
        _auditManager = auditManager;
        _sizeRepository = sizeRepository;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<EsitoManutenzione> EseguiAsync(CancellationToken cancellationToken = default)
    {
        // 1. Retention temporale (idempotente).
        var eliminate = await _auditManager.PurgaPrecedentiAsync(_options.MesiConservazione, cancellationToken);
        if (eliminate > 0)
        {
            _logger.LogInformation(
                "Retention audit: eliminate {Eliminate} righe più vecchie di {Mesi} mesi.",
                eliminate, _options.MesiConservazione);
        }

        // 2. Sentinella sulla dimensione dei file dati.
        var stato = await ValutaDimensioneAsync(cancellationToken);
        if (stato.Allarme)
        {
            // Warning (Livello 3): il DbLoggerProvider lo persiste su fatt.Log,
            // così l'amministratore lo vede in /admin/log. La sentinella avvisa,
            // non rimedia: spetta all'admin valutare retention/archiviazione.
            _logger.LogWarning(
                "Database a {DimensioneMb} MB, oltre la soglia di allarme di {SogliaGb} GB " +
                "(tetto SQL Express 10 GB). Valutare una retention più stretta o l'archiviazione " +
                "dei dati storici (audit/log).",
                stato.DimensioneDatiMb, stato.SogliaGb);
        }

        return new EsitoManutenzione(eliminate, stato.DimensioneDatiMb, stato.Allarme);
    }

    public async Task<StatoDimensione> ValutaDimensioneAsync(CancellationToken cancellationToken = default)
    {
        var dimensioneMb = await _sizeRepository.GetDimensioneDatiMbAsync(cancellationToken);
        var allarme = dimensioneMb >= _options.SogliaAllarmeGb * 1024;
        return new StatoDimensione(dimensioneMb, _options.SogliaAllarmeGb, allarme);
    }
}
