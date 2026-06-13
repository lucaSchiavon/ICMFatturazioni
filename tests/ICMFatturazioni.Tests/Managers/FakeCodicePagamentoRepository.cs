using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Models;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// In-memory di <see cref="ICodicePagamentoRepository"/> per i test (no DB). La
/// vista "ricca" è costruita in modo minimale (il tipo non è risolto: i test del
/// manager non ne verificano la descrizione).
/// </summary>
internal sealed class FakeCodicePagamentoRepository : ICodicePagamentoRepository
{
    private readonly Dictionary<Guid, CodicePagamento> _store = new();

    public HashSet<Guid> DipendenzeDa { get; } = new();

    private static CodicePagamentoRiga ToRiga(CodicePagamento c) => new(
        c.IdCodicePagamento, c.IdTipoPagamento, TipoDescrizione: string.Empty, FlagBanca.Azienda,
        c.DescrPag, c.NumScadenze, c.GGScad1, c.GGScad2, c.GGScad3, c.GGpiu, c.FineMese,
        c.CondizionePagamento, null, c.ModalitaPagamento, null, c.IsAttivo);

    public Task<IReadOnlyList<CodicePagamentoRiga>> GetAttiviAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CodicePagamentoRiga>>(
            _store.Values.Where(c => c.IsAttivo).OrderBy(c => c.DescrPag, StringComparer.OrdinalIgnoreCase).Select(ToRiga).ToList());

    public Task<CodicePagamento?> GetByIdAsync(Guid idCodicePagamento, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue(idCodicePagamento, out var c) ? c : null);

    public Task<bool> ExistsDescrizioneAttivaAsync(string descrizione, Guid? escludiId, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.Values.Any(c =>
            c.IsAttivo
            && string.Equals(c.DescrPag, descrizione, StringComparison.OrdinalIgnoreCase)
            && (escludiId is null || c.IdCodicePagamento != escludiId)));

    public Task InsertAsync(CodicePagamento codice, CancellationToken cancellationToken = default)
    {
        _store[codice.IdCodicePagamento] = codice;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(CodicePagamento codice, CancellationToken cancellationToken = default)
    {
        _store[codice.IdCodicePagamento] = codice;
        return Task.CompletedTask;
    }

    public Task DisattivaAsync(Guid idCodicePagamento, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(idCodicePagamento, out var c))
        {
            _store[idCodicePagamento] = new CodicePagamento
            {
                IdCodicePagamento = c.IdCodicePagamento,
                IdTipoPagamento = c.IdTipoPagamento,
                DescrPag = c.DescrPag,
                NumScadenze = c.NumScadenze,
                GGScad1 = c.GGScad1,
                GGScad2 = c.GGScad2,
                GGScad3 = c.GGScad3,
                GGpiu = c.GGpiu,
                FineMese = c.FineMese,
                CondizionePagamento = c.CondizionePagamento,
                ModalitaPagamento = c.ModalitaPagamento,
                IsAttivo = false,
            };
        }
        return Task.CompletedTask;
    }

    public Task<bool> HasDipendenzeAsync(Guid idCodicePagamento, CancellationToken cancellationToken = default)
        => Task.FromResult(DipendenzeDa.Contains(idCodicePagamento));
}
