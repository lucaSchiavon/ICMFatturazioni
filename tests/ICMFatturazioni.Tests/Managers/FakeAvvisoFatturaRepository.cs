using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Implementazione in-memory di <see cref="IAvvisoFatturaRepository"/> per i test
/// unitari del Manager. Simula l'aggregate write in modo osservabile: registra le
/// spese collegate e le rate consumate, e riproduce la guardia dell'indice univoco
/// (una rata in due avvisi → <see cref="AvvisoFatturaInvalidaException"/>).
/// </summary>
internal sealed class FakeAvvisoFatturaRepository : IAvvisoFatturaRepository
{
    private readonly Dictionary<Guid, AvvisoFattura> _testate = new();
    private readonly Dictionary<Guid, List<AvvisoFatturaRiga>> _righe = new();

    /// <summary>Spese collegate per avviso (osservabile dai test).</summary>
    public Dictionary<Guid, List<Guid>> SpeseCollegate { get; } = new();

    /// <summary>Rate attualmente consumate (IdScadenza → IdRiga), tra tutti gli avvisi attivi.</summary>
    public Dictionary<Guid, Guid> ScadenzeConsumate { get; } = new();

    public Task<IReadOnlyList<AvvisoFattura>> GetByAttivitaAsync(Guid idAttivita, CancellationToken ct = default)
    {
        var result = _testate.Values
            .Where(a => a.IdAttivita == idAttivita && a.IsAttivo)
            .OrderByDescending(a => a.DataAvviso)
            .Select(WithTotale)
            .ToList();
        return Task.FromResult<IReadOnlyList<AvvisoFattura>>(result);
    }

    public Task<AvvisoFattura?> GetByIdAsync(Guid idAvviso, CancellationToken ct = default)
        => Task.FromResult(_testate.TryGetValue(idAvviso, out var a) ? WithTotale(a) : null);

    public Task<IReadOnlyList<ICMFatturazioni.Web.Models.AttivitaFatturabile>> GetAttivitaConAvvisiNonFatturatiAsync(CancellationToken ct = default)
    {
        var result = _testate.Values
            .Where(a => a.IsAttivo && a.IdFattura is null)
            .Select(a => new ICMFatturazioni.Web.Models.AttivitaFatturabile(a.IdAnagrafica, a.IdAttivita))
            .Distinct()
            .ToList();
        return Task.FromResult<IReadOnlyList<ICMFatturazioni.Web.Models.AttivitaFatturabile>>(result);
    }

    // Stub: le grandezze cross-avviso derivano da dati non modellati nel fake
    // (importo totale del dettaglio). Nessun test le esercita → lista vuota.
    public Task<IReadOnlyList<ICMFatturazioni.Web.Models.DettaglioAvvisoGrandezze>> GetDettagliGrandezzeByAvvisoAsync(Guid idAvviso, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ICMFatturazioni.Web.Models.DettaglioAvvisoGrandezze>>(
            Array.Empty<ICMFatturazioni.Web.Models.DettaglioAvvisoGrandezze>());

    public Task<IReadOnlyList<AvvisoFatturaRiga>> GetRigheByAvvisoAsync(Guid idAvviso, CancellationToken ct = default)
    {
        var righe = _righe.TryGetValue(idAvviso, out var list)
            ? list.OrderBy(r => r.Ordine).ToList()
            : new List<AvvisoFatturaRiga>();
        return Task.FromResult<IReadOnlyList<AvvisoFatturaRiga>>(righe);
    }

    public Task EmettiAsync(
        AvvisoFattura testata,
        IReadOnlyList<AvvisoFatturaRiga> righe,
        IReadOnlyList<Guid> idSpeseCollegate,
        CancellationToken ct = default)
    {
        // Guardia indice univoco: una rata non può stare in due avvisi.
        foreach (var r in righe.Where(x => x.IdScadenza.HasValue))
        {
            if (ScadenzeConsumate.ContainsKey(r.IdScadenza!.Value))
                throw new AvvisoFatturaInvalidaException(
                    AvvisoFatturaMotivoInvalido.ScadenzaGiaInAvviso,
                    "Rata già in un altro avviso.");
        }

        _testate[testata.IdAvviso] = testata;
        _righe[testata.IdAvviso]   = righe.ToList();
        SpeseCollegate[testata.IdAvviso] = idSpeseCollegate.ToList();
        foreach (var r in righe.Where(x => x.IdScadenza.HasValue))
            ScadenzeConsumate[r.IdScadenza!.Value] = r.IdRiga;

        return Task.CompletedTask;
    }

    public Task AnnullaAsync(Guid idAvviso, CancellationToken ct = default)
    {
        if (!_testate.TryGetValue(idAvviso, out var a)) return Task.CompletedTask;

        // Sblocca le rate consumate da questo avviso.
        if (_righe.TryGetValue(idAvviso, out var righe))
            foreach (var r in righe.Where(x => x.IdScadenza.HasValue))
                ScadenzeConsumate.Remove(r.IdScadenza!.Value);

        _righe.Remove(idAvviso);
        SpeseCollegate.Remove(idAvviso);
        _testate[idAvviso] = CloneInattiva(a);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(AvvisoFattura avviso, CancellationToken ct = default)
    {
        if (_testate.ContainsKey(avviso.IdAvviso))
            _testate[avviso.IdAvviso] = avviso;
        return Task.CompletedTask;
    }

    public Task AggiornaRigheAsync(
        Guid idAvviso,
        IReadOnlyList<AvvisoFatturaRiga> nuoveRighe,
        IReadOnlyList<Guid> idSpeseCollegate,
        CancellationToken ct = default)
    {
        // Sblocca le rate correnti dell'avviso (replace-all).
        if (_righe.TryGetValue(idAvviso, out var vecchie))
            foreach (var r in vecchie.Where(x => x.IdScadenza.HasValue))
                ScadenzeConsumate.Remove(r.IdScadenza!.Value);

        // Guardia indice univoco: le nuove rate non devono essere di altri avvisi.
        foreach (var r in nuoveRighe.Where(x => x.IdScadenza.HasValue))
            if (ScadenzeConsumate.ContainsKey(r.IdScadenza!.Value))
                throw new AvvisoFatturaInvalidaException(
                    AvvisoFatturaMotivoInvalido.ScadenzaGiaInAvviso, "Rata già in un altro avviso.");

        _righe[idAvviso] = nuoveRighe.ToList();
        foreach (var r in nuoveRighe.Where(x => x.IdScadenza.HasValue))
            ScadenzeConsumate[r.IdScadenza!.Value] = r.IdRiga;

        // Riconcilia le spese collegate (replace-all).
        SpeseCollegate[idAvviso] = idSpeseCollegate.ToList();
        return Task.CompletedTask;
    }

    // ── Helper di test ──────────────────────────────────────────────────────

    /// <summary>Inserisce direttamente una testata (per i test che non emettono).</summary>
    public void Seed(AvvisoFattura a) => _testate[a.IdAvviso] = a;

    /// <summary>Simula la fatturazione di un avviso valorizzandone le nav-prop fattura.</summary>
    public void MarkFatturato(Guid idAvviso, Guid idFattura, int numero, int anno)
    {
        if (!_testate.TryGetValue(idAvviso, out var a)) return;
        a.IdFattura     = idFattura;
        a.NumeroFattura = numero;
        a.AnnoFattura   = anno;
    }

    // La somma delle righe è una nav-prop calcolata: la valorizziamo in lettura.
    private AvvisoFattura WithTotale(AvvisoFattura a)
    {
        a.TotaleRighe = _righe.TryGetValue(a.IdAvviso, out var list)
            ? list.Sum(r => r.Importo ?? 0m)
            : 0m;
        return a;
    }

    private static AvvisoFattura CloneInattiva(AvvisoFattura src) => new()
    {
        IdAvviso                 = src.IdAvviso,
        IdAttivita               = src.IdAttivita,
        IdAnagrafica             = src.IdAnagrafica,
        DataAvviso               = src.DataAvviso,
        Oggetto                  = src.Oggetto,
        NotaSintetica            = src.NotaSintetica,
        NotaTestata              = src.NotaTestata,
        IdCodicePagamento        = src.IdCodicePagamento,
        IdBancaAppoggio          = src.IdBancaAppoggio,
        AliquotaIva              = src.AliquotaIva,
        AliquotaCnpaia           = src.AliquotaCnpaia,
        AliquotaRitenuta         = src.AliquotaRitenuta,
        ApplicaRitenuta          = src.ApplicaRitenuta,
        DescrizioneSpeseInAvviso = src.DescrizioneSpeseInAvviso,
        IsAttivo                 = false,
    };
}
