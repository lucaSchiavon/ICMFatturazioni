using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Models;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Implementazione in-memory di <see cref="IScadenzaPagamentoRepository"/> per i test unitari.
/// </summary>
internal sealed class FakeScadenzaPagamentoRepository : IScadenzaPagamentoRepository
{
    private readonly List<ScadenzaPagamento> _store = new();

    /// <summary>
    /// Scadenze "fatturabili" seminate dai test (read-model). Il fake le restituisce
    /// per attività così com'è (nei test l'attività è unica), simulando la query reale.
    /// </summary>
    public List<ScadenzaFatturabile> Fatturabili { get; } = new();

    public Task<IReadOnlyList<ScadenzaFatturabile>> GetFatturabiliByAttivitaAsync(Guid idAttivita, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ScadenzaFatturabile>>(Fatturabili.ToList());

    /// <summary>Attività con residuo da fatturare seminate dai test (filtri maschera Avvisi).</summary>
    public List<AttivitaFatturabile> AttivitaFatturabili { get; } = new();

    public Task<IReadOnlyList<AttivitaFatturabile>> GetAttivitaConResiduoDaFatturareAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AttivitaFatturabile>>(AttivitaFatturabili.ToList());

    /// <summary>Dettagli da schedulare seminati dai test (segnalazione buchi in maschera Avvisi).</summary>
    public List<DettaglioDaSchedulare> DettagliDaSchedulare { get; } = new();

    public Task<IReadOnlyList<DettaglioDaSchedulare>> GetDettagliNonSchedulatiByAttivitaAsync(Guid idAttivita, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<DettaglioDaSchedulare>>(DettagliDaSchedulare.ToList());

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
