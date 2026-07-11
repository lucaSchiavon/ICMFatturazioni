using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Models;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// In-memory di <see cref="IAttivitaConsulenteRepository"/> per i test (no DB).
/// Replica anche il sentinel D-C2: DisattivaAsync non tocca la riga se ha pagamenti.
/// </summary>
internal sealed class FakeAttivitaConsulenteRepository : IAttivitaConsulenteRepository
{
    private readonly Dictionary<Guid, AttivitaConsulente> _store = new();

    /// <summary>Pagato per riga (simula le tranche attive di fatt.AttivitaConsulentiPagamenti).</summary>
    public Dictionary<Guid, decimal> PagatoPerRiga { get; } = new();

    /// <summary>Anagrafica/attività simulate per la scheda consulente (idAttivita → dati di join).</summary>
    public Dictionary<Guid, (Guid IdAnagrafica, string RagioneSociale, string Numero, string Descrizione)> AttivitaInfo { get; } = new();

    public Task<IReadOnlyList<SchedaConsulenzaRiga>> GetSchedaConsulenteAsync(Guid idConsulente, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SchedaConsulenzaRiga>>(
            _store.Values
                .Where(r => r.IdConsulente == idConsulente && r.IsAttivo)
                .Select(r =>
                {
                    var info = AttivitaInfo.TryGetValue(r.IdAttivita, out var i)
                        ? i : (IdAnagrafica: Guid.Empty, RagioneSociale: "Cliente?", Numero: "?", Descrizione: "?");
                    return new SchedaConsulenzaRiga
                    {
                        IdAttivitaConsulente = r.IdAttivitaConsulente,
                        IdAnagrafica         = info.IdAnagrafica,
                        IdAttivita           = r.IdAttivita,
                        RagioneSociale       = info.RagioneSociale,
                        AttivitaNumero       = info.Numero,
                        AttivitaDescrizione  = info.Descrizione,
                        TipoDescrizione      = r.TipoAttivitaConsulenteDescrizione ?? string.Empty,
                        Carico               = r.Carico,
                        Scadenza             = r.Scadenza,
                        Importo              = r.Importo,
                        Pagato               = PagatoPerRiga.TryGetValue(r.IdAttivitaConsulente, out var p) ? p : 0m,
                        Nota                 = r.Nota,
                    };
                })
                .OrderBy(s => s.RagioneSociale, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.AttivitaNumero, StringComparer.OrdinalIgnoreCase)
                .ToList());

    public Task<IReadOnlyList<AttivitaConsulente>> GetByAttivitaAsync(Guid idAttivita, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AttivitaConsulente>>(
            _store.Values
                .Where(r => r.IdAttivita == idAttivita && r.IsAttivo)
                .OrderBy(r => r.ConsulenteDescrizione, StringComparer.OrdinalIgnoreCase)
                .ToList());

    public Task<AttivitaConsulente?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue(id, out var r) ? r : null);

    public Task InsertAsync(AttivitaConsulente riga, CancellationToken cancellationToken = default)
    {
        _store[riga.IdAttivitaConsulente] = riga;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(AttivitaConsulente riga, CancellationToken cancellationToken = default)
    {
        // Come lo SqlUpdate reale: IdAttivita non cambia.
        if (_store.TryGetValue(riga.IdAttivitaConsulente, out var esistente))
            _store[riga.IdAttivitaConsulente] = Clone(riga, idAttivita: esistente.IdAttivita, isAttivo: esistente.IsAttivo);
        return Task.CompletedTask;
    }

    public Task DisattivaAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Sentinel D-C2: nessuna disattivazione se la riga ha pagamenti attivi.
        if (_store.TryGetValue(id, out var r) && GetPagato(id) == 0)
            _store[id] = Clone(r, isAttivo: false);
        return Task.CompletedTask;
    }

    public Task<bool> HasPagamentiAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(GetPagato(id) > 0);

    public Task<decimal> GetPagatoAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(GetPagato(id));

    private decimal GetPagato(Guid id)
        => PagatoPerRiga.TryGetValue(id, out var p) ? p : 0m;

    private static AttivitaConsulente Clone(AttivitaConsulente r, Guid? idAttivita = null, bool? isAttivo = null) => new()
    {
        IdAttivitaConsulente     = r.IdAttivitaConsulente,
        IdAttivita               = idAttivita ?? r.IdAttivita,
        IdConsulente             = r.IdConsulente,
        IdTipoAttivitaConsulente = r.IdTipoAttivitaConsulente,
        Carico                   = r.Carico,
        Importo                  = r.Importo,
        Scadenza                 = r.Scadenza,
        Nota                     = r.Nota,
        IsAttivo                 = isAttivo ?? r.IsAttivo,
        ConsulenteDescrizione             = r.ConsulenteDescrizione,
        TipoAttivitaConsulenteDescrizione = r.TipoAttivitaConsulenteDescrizione,
    };
}
