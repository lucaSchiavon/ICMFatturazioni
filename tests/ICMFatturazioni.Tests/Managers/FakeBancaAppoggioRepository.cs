using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Models;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// In-memory di <see cref="IBancaAppoggioRepository"/> per i test. Risolve i
/// nomi/codici di banca e agenzia attingendo ai fake di
/// <see cref="FakeBancaRepository"/>/<see cref="FakeAgenziaRepository"/> (come fa
/// il JOIN reale), così le letture restituiscono <see cref="BancaAppoggioRiga"/>.
/// </summary>
internal sealed class FakeBancaAppoggioRepository : IBancaAppoggioRepository
{
    private readonly FakeBancaRepository _banche;
    private readonly FakeAgenziaRepository _agenzie;
    private readonly Dictionary<Guid, BancaAppoggio> _store = new();

    public HashSet<Guid> DipendenzeDa { get; } = new();

    public FakeBancaAppoggioRepository(FakeBancaRepository banche, FakeAgenziaRepository agenzie)
    {
        _banche = banche;
        _agenzie = agenzie;
    }

    private BancaAppoggioRiga ToRiga(BancaAppoggio e)
    {
        var banca = _banche.Store.GetValueOrDefault(e.IdBanca);
        var agenzia = e.IdAgenzia is null ? null : _agenzie.Store.GetValueOrDefault(e.IdAgenzia.Value);
        return new BancaAppoggioRiga(
            e.IdBancaAppoggio, e.IdCliente, e.IdBanca,
            banca?.Nome ?? string.Empty, banca?.ABI,
            e.IdAgenzia, agenzia?.Nome, agenzia?.CAB,
            e.IBAN, e.IsAttivo);
    }

    public Task<IReadOnlyList<BancaAppoggioRiga>> GetAttiviAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<BancaAppoggioRiga>>(
            _store.Values.Where(b => b.IsAttivo)
                .OrderBy(b => b.IsBancaAzienda ? 0 : 1)
                .Select(ToRiga)
                .OrderBy(r => r.IsBancaAzienda ? 0 : 1)
                .ThenBy(r => r.BancaNome, StringComparer.OrdinalIgnoreCase)
                .ToList());

    public Task<BancaAppoggioRiga?> GetByIdAsync(Guid idBancaAppoggio, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue(idBancaAppoggio, out var e) ? ToRiga(e) : null);

    public Task<IReadOnlyList<BancaAppoggioRiga>> GetSelezionabiliAsync(Guid? idCliente, bool bancheAzienda, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<BancaAppoggioRiga>>(
            _store.Values.Where(b => b.IsAttivo)
                .Where(b => bancheAzienda ? b.IdCliente is null : idCliente is not null && b.IdCliente == idCliente)
                .Select(ToRiga)
                .OrderBy(r => r.BancaNome, StringComparer.OrdinalIgnoreCase)
                .ToList());

    public Task<bool> ExistsLegameAttivoAsync(Guid? idCliente, Guid idBanca, Guid? idAgenzia, Guid? escludiId, CancellationToken cancellationToken = default)
    {
        if (idAgenzia is null)
        {
            return Task.FromResult(false);
        }
        return Task.FromResult(_store.Values.Any(b =>
            b.IsAttivo
            && b.IdBanca == idBanca
            && b.IdAgenzia == idAgenzia
            && b.IdCliente == idCliente
            && (escludiId is null || b.IdBancaAppoggio != escludiId)));
    }

    public Task InsertAsync(BancaAppoggio banca, CancellationToken cancellationToken = default)
    {
        _store[banca.IdBancaAppoggio] = banca;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(BancaAppoggio banca, CancellationToken cancellationToken = default)
    {
        _store[banca.IdBancaAppoggio] = banca;
        return Task.CompletedTask;
    }

    public Task DisattivaAsync(Guid idBancaAppoggio, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(idBancaAppoggio, out var e))
        {
            _store[idBancaAppoggio] = new BancaAppoggio
            {
                IdBancaAppoggio = e.IdBancaAppoggio,
                IdCliente = e.IdCliente,
                IdBanca = e.IdBanca,
                IdAgenzia = e.IdAgenzia,
                IBAN = e.IBAN,
                IsAttivo = false,
            };
        }
        return Task.CompletedTask;
    }

    public Task<bool> HasDipendenzeAsync(Guid idBancaAppoggio, CancellationToken cancellationToken = default)
        => Task.FromResult(DipendenzeDa.Contains(idBancaAppoggio));
}
