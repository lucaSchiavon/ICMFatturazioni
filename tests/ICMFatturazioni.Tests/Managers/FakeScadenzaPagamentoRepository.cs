using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Implementazione in-memory di <see cref="IScadenzaPagamentoRepository"/> per i test unitari.
/// </summary>
internal sealed class FakeScadenzaPagamentoRepository : IScadenzaPagamentoRepository
{
    private readonly List<ScadenzaPagamento> _store = new();

    public Task<IReadOnlyList<ScadenzaPagamento>> GetByDettaglioAsync(Guid idAttivitaDettaglio, CancellationToken ct = default)
    {
        var result = _store
            .Where(s => s.IdAttivitaDettaglio == idAttivitaDettaglio && s.IsAttivo)
            .OrderBy(s => s.DataScadenza)
            .ToList();
        return Task.FromResult<IReadOnlyList<ScadenzaPagamento>>(result);
    }

    public Task<ScadenzaPagamento?> GetByIdAsync(Guid idScadenza, CancellationToken ct = default)
    {
        var s = _store.FirstOrDefault(x => x.IdScadenza == idScadenza);
        return Task.FromResult<ScadenzaPagamento?>(s);
    }

    public Task InsertAsync(ScadenzaPagamento scadenza, CancellationToken ct = default)
    {
        _store.Add(scadenza);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ScadenzaPagamento scadenza, CancellationToken ct = default)
    {
        var idx = _store.FindIndex(s => s.IdScadenza == scadenza.IdScadenza);
        if (idx >= 0) _store[idx] = scadenza;
        return Task.CompletedTask;
    }

    public Task DisattivaAsync(Guid idScadenza, CancellationToken ct = default)
    {
        var idx = _store.FindIndex(s => s.IdScadenza == idScadenza);
        if (idx < 0) return Task.CompletedTask;
        var s = _store[idx];
        _store[idx] = new ScadenzaPagamento
        {
            IdScadenza          = s.IdScadenza,
            IdAttivitaDettaglio = s.IdAttivitaDettaglio,
            DataScadenza        = s.DataScadenza,
            Importo             = s.Importo,
            Nota                = s.Nota,
            IsAttivo            = false,
        };
        return Task.CompletedTask;
    }
}
