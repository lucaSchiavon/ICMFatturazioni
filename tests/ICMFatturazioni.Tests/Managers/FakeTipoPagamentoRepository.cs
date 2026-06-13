using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>In-memory di <see cref="ITipoPagamentoRepository"/> per i test (no DB).</summary>
internal sealed class FakeTipoPagamentoRepository : ITipoPagamentoRepository
{
    private readonly Dictionary<Guid, TipoPagamento> _store = new();

    /// <summary>Id che il fake dichiarerà "con dipendenze" (codici figli).</summary>
    public HashSet<Guid> DipendenzeDa { get; } = new();

    public Task<IReadOnlyList<TipoPagamento>> GetAttiviAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TipoPagamento>>(
            _store.Values.Where(t => t.IsAttivo).OrderBy(t => t.Descrizione, StringComparer.OrdinalIgnoreCase).ToList());

    public Task<TipoPagamento?> GetByIdAsync(Guid idTipoPagamento, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue(idTipoPagamento, out var t) ? t : null);

    public Task<bool> ExistsDescrizioneAttivaAsync(string descrizione, Guid? escludiId, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.Values.Any(t =>
            t.IsAttivo
            && string.Equals(t.Descrizione, descrizione, StringComparison.OrdinalIgnoreCase)
            && (escludiId is null || t.IdTipoPagamento != escludiId)));

    public Task<bool> ExistsSiglaAttivaAsync(string? siglaPag, Guid? escludiId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(siglaPag))
        {
            return Task.FromResult(false);
        }
        return Task.FromResult(_store.Values.Any(t =>
            t.IsAttivo
            && string.Equals(t.SiglaPag, siglaPag, StringComparison.OrdinalIgnoreCase)
            && (escludiId is null || t.IdTipoPagamento != escludiId)));
    }

    public Task InsertAsync(TipoPagamento tipo, CancellationToken cancellationToken = default)
    {
        _store[tipo.IdTipoPagamento] = tipo;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(TipoPagamento tipo, CancellationToken cancellationToken = default)
    {
        _store[tipo.IdTipoPagamento] = tipo;
        return Task.CompletedTask;
    }

    public Task DisattivaAsync(Guid idTipoPagamento, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(idTipoPagamento, out var t))
        {
            _store[idTipoPagamento] = new TipoPagamento
            {
                IdTipoPagamento = t.IdTipoPagamento,
                Descrizione = t.Descrizione,
                SiglaPag = t.SiglaPag,
                FlagBanca = t.FlagBanca,
                IsAttivo = false,
            };
        }
        return Task.CompletedTask;
    }

    public Task<bool> HasDipendenzeAsync(Guid idTipoPagamento, CancellationToken cancellationToken = default)
        => Task.FromResult(DipendenzeDa.Contains(idTipoPagamento));
}
