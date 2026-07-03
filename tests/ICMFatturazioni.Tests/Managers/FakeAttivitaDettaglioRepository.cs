using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Implementazione in-memory di <see cref="IAttivitaDettaglioRepository"/> per i test unitari.
/// ScambiaOrdineAsync scambia gli ordini in-memory senza il workaround -999 (non serve in memoria).
/// </summary>
internal sealed class FakeAttivitaDettaglioRepository : IAttivitaDettaglioRepository
{
    private readonly List<AttivitaDettaglio> _store = new();

    // Consente ai test di controllare direttamente lo stato HasFattura.
    public void SetHasFattura(Guid idAttivitaDettaglio, bool hasFattura)
    {
        var idx = _store.FindIndex(d => d.IdAttivitaDettaglio == idAttivitaDettaglio);
        if (idx < 0) return;
        var d = _store[idx];
        _store[idx] = new AttivitaDettaglio
        {
            IdAttivitaDettaglio     = d.IdAttivitaDettaglio,
            IdAttivita              = d.IdAttivita,
            IdTipoDettaglioAttivita = d.IdTipoDettaglioAttivita,
            Ordine                  = d.Ordine,
            DescrizioneDettaglio    = d.DescrizioneDettaglio,
            Importo                 = d.Importo,
            NotaDettaglio           = d.NotaDettaglio,
            TerminePrevisto         = d.TerminePrevisto,
            HasFattura              = hasFattura,
            IsAttivo                = d.IsAttivo,
        };
    }

    public Task<IReadOnlyList<AttivitaDettaglio>> GetByAttivitaAsync(Guid idAttivita, CancellationToken ct = default)
    {
        var result = _store
            .Where(d => d.IdAttivita == idAttivita && d.IsAttivo)
            .OrderBy(d => d.Ordine)
            .ToList();
        return Task.FromResult<IReadOnlyList<AttivitaDettaglio>>(result);
    }

    public Task<AttivitaDettaglio?> GetByIdAsync(Guid idAttivitaDettaglio, CancellationToken ct = default)
    {
        var d = _store.FirstOrDefault(x => x.IdAttivitaDettaglio == idAttivitaDettaglio);
        return Task.FromResult<AttivitaDettaglio?>(d);
    }

    public Task<int> GetMaxOrdineAsync(Guid idAttivita, CancellationToken ct = default)
    {
        // Come nel repo reale: MAX su TUTTE le righe (attive e soft-deletate),
        // perché il vincolo UNIQUE (IdAttivita, Ordine) non è filtrato su IsAttivo.
        var max = _store
            .Where(d => d.IdAttivita == idAttivita)
            .Select(d => (int?)d.Ordine)
            .Max() ?? 0;
        return Task.FromResult(max);
    }

    public Task InsertAsync(AttivitaDettaglio dettaglio, CancellationToken ct = default)
    {
        _store.Add(dettaglio);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(AttivitaDettaglio dettaglio, CancellationToken ct = default)
    {
        var idx = _store.FindIndex(d => d.IdAttivitaDettaglio == dettaglio.IdAttivitaDettaglio);
        if (idx >= 0) _store[idx] = dettaglio;
        return Task.CompletedTask;
    }

    public Task DisattivaAsync(Guid idAttivitaDettaglio, CancellationToken ct = default)
    {
        var idx = _store.FindIndex(d => d.IdAttivitaDettaglio == idAttivitaDettaglio);
        if (idx < 0) return Task.CompletedTask;
        var d = _store[idx];
        _store[idx] = new AttivitaDettaglio
        {
            IdAttivitaDettaglio     = d.IdAttivitaDettaglio,
            IdAttivita              = d.IdAttivita,
            IdTipoDettaglioAttivita = d.IdTipoDettaglioAttivita,
            Ordine                  = d.Ordine,
            DescrizioneDettaglio    = d.DescrizioneDettaglio,
            Importo                 = d.Importo,
            NotaDettaglio           = d.NotaDettaglio,
            TerminePrevisto         = d.TerminePrevisto,
            HasFattura              = d.HasFattura,
            IsAttivo                = false,
        };
        return Task.CompletedTask;
    }

    public Task ScambiaOrdineAsync(Guid idA, Guid idB, int ordineA, int ordineB, CancellationToken ct = default)
    {
        var idxA = _store.FindIndex(d => d.IdAttivitaDettaglio == idA);
        var idxB = _store.FindIndex(d => d.IdAttivitaDettaglio == idB);
        if (idxA < 0 || idxB < 0) return Task.CompletedTask;

        var a = _store[idxA];
        var b = _store[idxB];

        // Ricreiamo le entity con gli ordini scambiati (init properties).
        _store[idxA] = new AttivitaDettaglio
        {
            IdAttivitaDettaglio     = a.IdAttivitaDettaglio,
            IdAttivita              = a.IdAttivita,
            IdTipoDettaglioAttivita = a.IdTipoDettaglioAttivita,
            Ordine                  = ordineB,
            DescrizioneDettaglio    = a.DescrizioneDettaglio,
            Importo                 = a.Importo,
            NotaDettaglio           = a.NotaDettaglio,
            TerminePrevisto         = a.TerminePrevisto,
            HasFattura              = a.HasFattura,
            IsAttivo                = a.IsAttivo,
        };
        _store[idxB] = new AttivitaDettaglio
        {
            IdAttivitaDettaglio     = b.IdAttivitaDettaglio,
            IdAttivita              = b.IdAttivita,
            IdTipoDettaglioAttivita = b.IdTipoDettaglioAttivita,
            Ordine                  = ordineA,
            DescrizioneDettaglio    = b.DescrizioneDettaglio,
            Importo                 = b.Importo,
            NotaDettaglio           = b.NotaDettaglio,
            TerminePrevisto         = b.TerminePrevisto,
            HasFattura              = b.HasFattura,
            IsAttivo                = b.IsAttivo,
        };
        return Task.CompletedTask;
    }
}
