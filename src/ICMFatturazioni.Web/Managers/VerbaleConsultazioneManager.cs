using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Models;
using ICMFatturazioni.Web.Repositories.Interfaces;
using ICMFatturazioni.Web.Services;

namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Implementazione di <see cref="IVerbaleConsultazioneManager"/>.
/// </summary>
/// <remarks>
/// Sola lettura: nessun audit (Regola 7 non si applica, non si modificano dati).
/// La regola di dominio chiave — mostrare solo verbali con PDF fisicamente
/// presente, senza mai rigenerarlo — è applicata qui filtrando i candidati del
/// repository con <see cref="IVerbaleReportStorage.Esiste"/>. Volumi piccoli:
/// si carica l'intero elenco esportabile una volta e si filtra in memoria.
/// </remarks>
internal sealed class VerbaleConsultazioneManager : IVerbaleConsultazioneManager
{
    private readonly IVerbaleConsultazioneRepository _repository;
    private readonly IVerbaleReportStorage _reportStorage;

    public VerbaleConsultazioneManager(
        IVerbaleConsultazioneRepository repository,
        IVerbaleReportStorage reportStorage)
    {
        _repository = repository;
        _reportStorage = reportStorage;
    }

    public async Task<IReadOnlyList<VerbaleConsultazione>> ElencoEsportabiliAsync(CancellationToken cancellationToken = default)
    {
        var candidati = await _repository.GetEsportabiliAsync(cancellationToken);
        // Filtro di esistenza fisica: un ReportPath valorizzato ma senza file su
        // disco è dato sporco/legacy → escluso (mai rigenerato).
        return candidati.Where(v => _reportStorage.Esiste(v.ReportPath)).ToList();
    }

    public async Task<IReadOnlyList<VerbaleConsultazione>> ElencoPerFiltroAsync(
        Guid idAnagrafica,
        Guid? idAttivita,
        Guid? idCantiere,
        CancellationToken cancellationToken = default)
    {
        var esportabili = await ElencoEsportabiliAsync(cancellationToken);
        return esportabili
            .Where(v => v.IdAnagrafica == idAnagrafica
                && (idAttivita is null || v.IdAttivita == idAttivita)
                && (idCantiere is null || v.IdCantiere == idCantiere))
            .ToList();
    }
}
