using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Fake di <see cref="IAttivitaManager"/> per i test dei manager che validano
/// il puntatore all'attività (es. <c>CantiereManager</c>). Espone solo la
/// lettura su uno store in-memory; le operazioni di scrittura non servono ai
/// consumatori e lanciano <see cref="NotSupportedException"/>.
/// </summary>
internal sealed class FakeAttivitaManager : IAttivitaManager
{
    /// <summary>Attività note al fake, indicizzate per id.</summary>
    public Dictionary<Guid, Attivita> Attivita { get; } = new();

    public Task<IReadOnlyList<Attivita>> ElencoAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Attivita>>(
            Attivita.Values.Where(a => a.IsAttivo).ToList());

    public Task<IReadOnlyList<Attivita>> ElencoPerAnagraficaAsync(Guid idAnagrafica, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Attivita>>(
            Attivita.Values.Where(a => a.IsAttivo && a.IdAnagrafica == idAnagrafica).ToList());

    public Task<IReadOnlyList<Attivita>> ElencoPerAnagraficaTipoAsync(Guid idAnagrafica, Guid idTipoAttivita, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Attivita>>(
            Attivita.Values.Where(a => a.IsAttivo && a.IdAnagrafica == idAnagrafica && a.IdTipoAttivita == idTipoAttivita).ToList());

    public Task<Attivita?> GetByIdAsync(Guid idAttivita, CancellationToken cancellationToken = default)
        => Task.FromResult(Attivita.TryGetValue(idAttivita, out var a) ? a : null);

    public Task<Guid> CreaAsync(Attivita attivita, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Fake in sola lettura.");

    public Task AggiornaAsync(Attivita attivita, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Fake in sola lettura.");

    public Task EliminaAsync(Guid idAttivita, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Fake in sola lettura.");

    public Task<bool> EEliminabileAsync(Guid idAttivita, CancellationToken cancellationToken = default)
        => Task.FromResult(true);
}
