using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Models;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// In-memory di <see cref="IAttivitaConsulentePagamentoRepository"/> per i test (no DB).
/// Le righe consulenza sono simulate con <see cref="AddRiga"/>; Pagato e Residuo
/// sono derivati dalle tranche attive come nelle query reali.
/// </summary>
internal sealed class FakeAttivitaConsulentePagamentoRepository : IAttivitaConsulentePagamentoRepository
{
    private sealed record RigaFake(Guid IdAttivita, string Consulente, string Tipo, DateOnly? Scadenza, decimal Importo, bool CaricoStudio, Guid? IdConsulente = null);

    private readonly Dictionary<Guid, RigaFake> _righe = new();
    private readonly Dictionary<Guid, AttivitaConsulentePagamento> _store = new();

    /// <summary>Registra una riga consulenza simulata e ne restituisce l'id.</summary>
    public Guid AddRiga(Guid idAttivita, decimal importo, bool caricoStudio = true,
        string consulente = "Luca Schiavon", string tipo = "CALCOLI STRUTTURALI", DateOnly? scadenza = null,
        Guid? idConsulente = null)
    {
        var id = Guid.CreateVersion7();
        _righe[id] = new RigaFake(idAttivita, consulente, tipo, scadenza, importo, caricoStudio, idConsulente);
        return id;
    }

    public Task<IReadOnlyList<AttivitaConsulentePagamento>> GetByConsulenteAsync(Guid? idConsulente, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AttivitaConsulentePagamento>>(
            _store.Values
                .Where(p => p.IsAttivo
                            && _righe.TryGetValue(p.IdAttivitaConsulente, out var r)
                            && (idConsulente is null || r.IdConsulente == idConsulente))
                .OrderBy(p => p.DataPagamento)
                .ToList());

    public Task<IReadOnlyList<ConsulenzaConSaldo>> GetConsulenzeConSaldoAsync(Guid idAttivita, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ConsulenzaConSaldo>>(
            _righe
                .Where(kv => kv.Value.IdAttivita == idAttivita && kv.Value.CaricoStudio)
                .OrderBy(kv => kv.Value.Consulente, StringComparer.OrdinalIgnoreCase)
                .Select(kv => new ConsulenzaConSaldo
                {
                    IdAttivitaConsulente  = kv.Key,
                    ConsulenteDescrizione = kv.Value.Consulente,
                    TipoDescrizione       = kv.Value.Tipo,
                    Scadenza              = kv.Value.Scadenza,
                    Importo               = kv.Value.Importo,
                    Pagato                = PagatoDi(kv.Key, escludi: null),
                })
                .ToList());

    public Task<IReadOnlyList<AttivitaConsulentePagamento>> GetByRigaAsync(Guid idAttivitaConsulente, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AttivitaConsulentePagamento>>(
            _store.Values
                .Where(p => p.IdAttivitaConsulente == idAttivitaConsulente && p.IsAttivo)
                .OrderBy(p => p.DataPagamento)
                .ToList());

    public Task<AttivitaConsulentePagamento?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue(id, out var p) ? p : null);

    public Task<SaldoRiga?> GetSaldoRigaAsync(Guid idAttivitaConsulente, Guid? escludiPagamento, CancellationToken cancellationToken = default)
        => Task.FromResult(_righe.TryGetValue(idAttivitaConsulente, out var r)
            ? new SaldoRiga
            {
                Importo      = r.Importo,
                Pagato       = PagatoDi(idAttivitaConsulente, escludiPagamento),
                CaricoStudio = r.CaricoStudio,
            }
            : null);

    public Task InsertAsync(AttivitaConsulentePagamento pagamento, CancellationToken cancellationToken = default)
    {
        _store[pagamento.IdConsulentePagamento] = pagamento;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(AttivitaConsulentePagamento pagamento, CancellationToken cancellationToken = default)
    {
        // Come lo SqlUpdate reale: IdAttivitaConsulente non cambia.
        if (_store.TryGetValue(pagamento.IdConsulentePagamento, out var esistente))
            _store[pagamento.IdConsulentePagamento] = Clone(pagamento,
                idRiga: esistente.IdAttivitaConsulente, isAttivo: esistente.IsAttivo);
        return Task.CompletedTask;
    }

    public Task DisattivaAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(id, out var p))
            _store[id] = Clone(p, isAttivo: false);
        return Task.CompletedTask;
    }

    private decimal PagatoDi(Guid idRiga, Guid? escludi)
        => _store.Values
            .Where(p => p.IdAttivitaConsulente == idRiga && p.IsAttivo
                        && (escludi is null || p.IdConsulentePagamento != escludi))
            .Sum(p => p.Importo);

    private static AttivitaConsulentePagamento Clone(AttivitaConsulentePagamento p, Guid? idRiga = null, bool? isAttivo = null) => new()
    {
        IdConsulentePagamento = p.IdConsulentePagamento,
        IdAttivitaConsulente  = idRiga ?? p.IdAttivitaConsulente,
        DataPagamento         = p.DataPagamento,
        Importo               = p.Importo,
        Nota                  = p.Nota,
        IsAttivo              = isAttivo ?? p.IsAttivo,
    };
}
