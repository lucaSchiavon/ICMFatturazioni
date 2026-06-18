using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>In-memory di <see cref="IAttivitaRepository"/> per i test (no DB).</summary>
internal sealed class FakeAttivitaRepository : IAttivitaRepository
{
    private readonly Dictionary<Guid, Attivita> _store = new();

    /// <summary>Id che il fake dichiarerà "con dipendenze" (dettagli figli).</summary>
    public HashSet<Guid> DipendenzeDa { get; } = new();

    public Task<IReadOnlyList<Attivita>> GetAttiviAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Attivita>>(
            _store.Values.Where(a => a.IsAttivo).OrderByDescending(a => a.Numero).ToList());

    public Task<IReadOnlyList<Attivita>> GetByAnagraficaAsync(Guid idAnagrafica, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Attivita>>(
            _store.Values.Where(a => a.IsAttivo && a.IdAnagrafica == idAnagrafica)
                .OrderByDescending(a => a.Numero).ToList());

    public Task<IReadOnlyList<Attivita>> GetByAnagraficaETipoAsync(Guid idAnagrafica, Guid idTipoAttivita, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Attivita>>(
            _store.Values.Where(a => a.IsAttivo && a.IdAnagrafica == idAnagrafica && a.IdTipoAttivita == idTipoAttivita)
                .OrderByDescending(a => a.Numero).ToList());

    public Task<Attivita?> GetByIdAsync(Guid idAttivita, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue(idAttivita, out var a) ? a : null);

    public Task InsertAsync(Attivita attivita, CancellationToken cancellationToken = default)
    {
        _store[attivita.IdAttivita] = attivita;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Attivita attivita, CancellationToken cancellationToken = default)
    {
        _store[attivita.IdAttivita] = attivita;
        return Task.CompletedTask;
    }

    public Task DisattivaAsync(Guid idAttivita, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(idAttivita, out var a))
        {
            _store[idAttivita] = new Attivita
            {
                IdAttivita          = a.IdAttivita,
                IdAnagrafica        = a.IdAnagrafica,
                IdTipoAttivita      = a.IdTipoAttivita,
                Numero              = a.Numero,
                Descrizione         = a.Descrizione,
                ProgettoDefinitivo  = a.ProgettoDefinitivo,
                ConcessioneEdilizia = a.ConcessioneEdilizia,
                InizioLavori        = a.InizioLavori,
                ImportoOpera        = a.ImportoOpera,
                IsAttivo            = false,
            };
        }
        return Task.CompletedTask;
    }

    public Task<bool> HasDipendenzeAsync(Guid idAttivita, CancellationToken cancellationToken = default)
        => Task.FromResult(DipendenzeDa.Contains(idAttivita));
}
