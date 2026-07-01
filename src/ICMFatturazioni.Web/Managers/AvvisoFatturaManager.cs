using ICMFatturazioni.Web.Auditing;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Models;
using ICMFatturazioni.Web.Repositories.Interfaces;
using ICMFatturazioni.Web.Services;

namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Implementazione di <see cref="IAvvisoFatturaManager"/>.
///
/// Responsabilità: validazione del comando, risoluzione degli <b>snapshot</b>
/// fiscali/pagamento all'emissione, costruzione di testata e righe con importi
/// autorevoli (letti dal DB, non digitati), delega dell'atomicità all'aggregate
/// write del repository e audit best-effort di ogni scrittura.
/// </summary>
public sealed class AvvisoFatturaManager : IAvvisoFatturaManager
{
    private readonly IAvvisoFatturaRepository     _repo;
    private readonly IScadenzaPagamentoRepository _scadenze;
    private readonly IAnagraficaRepository        _anagrafiche;
    private readonly IAliquotaManager             _aliquote;
    private readonly ICalcoloFiscaleAvviso        _calcolo;
    private readonly IAuditManager                _audit;

    public AvvisoFatturaManager(
        IAvvisoFatturaRepository repo,
        IScadenzaPagamentoRepository scadenze,
        IAnagraficaRepository anagrafiche,
        IAliquotaManager aliquote,
        ICalcoloFiscaleAvviso calcolo,
        IAuditManager audit)
    {
        _repo        = repo;
        _scadenze    = scadenze;
        _anagrafiche = anagrafiche;
        _aliquote    = aliquote;
        _calcolo     = calcolo;
        _audit       = audit;
    }

    // -----------------------------------------------------------------------
    // Letture
    // -----------------------------------------------------------------------

    public Task<IReadOnlyList<AvvisoFattura>> ElencoPerAttivitaAsync(Guid idAttivita, CancellationToken ct = default)
        => _repo.GetByAttivitaAsync(idAttivita, ct);

    public async Task<AvvisoDettaglio?> GetDettaglioAsync(Guid idAvviso, CancellationToken ct = default)
    {
        var testata = await _repo.GetByIdAsync(idAvviso, ct);
        if (testata is null) return null;
        var righe = await _repo.GetRigheByAvvisoAsync(idAvviso, ct);
        return new AvvisoDettaglio(testata, righe);
    }

    public Task<IReadOnlyList<ScadenzaFatturabile>> ScadenzeFatturabiliAsync(Guid idAttivita, CancellationToken ct = default)
        => _scadenze.GetFatturabiliByAttivitaAsync(idAttivita, ct);

    public Task<IReadOnlyList<AttivitaFatturabile>> AttivitaFatturabiliAsync(CancellationToken ct = default)
        => _scadenze.GetAttivitaConResiduoDaFatturareAsync(ct);

    public Task<IReadOnlyList<DettaglioDaSchedulare>> DettagliDaSchedulareAsync(Guid idAttivita, CancellationToken ct = default)
        => _scadenze.GetDettagliNonSchedulatiByAttivitaAsync(idAttivita, ct);

    // -----------------------------------------------------------------------
    // Emissione (atomica)
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<Guid> EmettiAsync(EmissioneAvvisoRequest request, CancellationToken ct = default)
    {
        // Validazioni "di flusso previsto".
        if (request.DataAvviso == default)
            throw new AvvisoFatturaInvalidaException(
                AvvisoFatturaMotivoInvalido.DataObbligatoria,
                "La data dell'avviso è obbligatoria.");

        if (request.IdScadenzeSelezionate.Count == 0)
            throw new AvvisoFatturaInvalidaException(
                AvvisoFatturaMotivoInvalido.NessunaScadenzaSelezionata,
                "Seleziona almeno una rata da fatturare.");

        var anagrafica = await _anagrafiche.GetByIdAsync(request.IdAnagrafica, ct)
            ?? throw new AvvisoFatturaInvalidaException(
                AvvisoFatturaMotivoInvalido.AnagraficaNonTrovata,
                "Cliente non trovato.");

        // Snapshot autorevole delle rate: la fatturabilità (esiste, attiva, non
        // consumata, dell'attività) è garantita dalla presenza in questa mappa.
        var fatturabili = (await _scadenze.GetFatturabiliByAttivitaAsync(request.IdAttivita, ct))
            .ToDictionary(f => f.IdScadenza);

        var aliquote = await _aliquote.GetAliquoteAvvisoAsync(ct);

        var idAvviso = Guid.CreateVersion7();
        var righe    = new List<AvvisoFatturaRiga>();
        var ordine   = 1;

        // Righe reali: una per scadenza selezionata, importo/etichette dal DB.
        foreach (var idScadenza in request.IdScadenzeSelezionate)
        {
            if (!fatturabili.TryGetValue(idScadenza, out var f))
                throw new AvvisoFatturaInvalidaException(
                    AvvisoFatturaMotivoInvalido.ScadenzaNonFatturabile,
                    "Una rata selezionata non è più disponibile: ricarica l'elenco delle scadenze.");

            righe.Add(new AvvisoFatturaRiga
            {
                IdRiga              = Guid.CreateVersion7(),
                IdAvviso            = idAvviso,
                Ordine              = ordine++,
                IdAttivitaDettaglio = f.IdAttivitaDettaglio,
                IdScadenza          = f.IdScadenza,
                Tipo                = f.TipoDettaglioDescrizione,
                Descrizione         = f.DescrizioneDettaglio,
                Importo             = f.Importo,
                IsDescrittiva       = false,
            });
        }

        // Righe descrittive facoltative, accodate.
        foreach (var descrittiva in request.RigheDescrittive ?? [])
        {
            righe.Add(new AvvisoFatturaRiga
            {
                IdRiga        = Guid.CreateVersion7(),
                IdAvviso      = idAvviso,
                Ordine        = ordine++,
                Descrizione   = descrittiva.Descrizione,
                IsDescrittiva = true,
            });
        }

        var testata = new AvvisoFattura
        {
            IdAvviso                 = idAvviso,
            IdAttivita               = request.IdAttivita,
            IdAnagrafica             = request.IdAnagrafica,
            DataAvviso               = request.DataAvviso,
            Oggetto                  = request.Oggetto,
            NotaSintetica            = request.NotaSintetica,
            NotaTestata              = request.NotaTestata,
            // Ereditati dall'anagrafica, salvo override esplicito sull'avviso.
            IdCodicePagamento        = request.IdCodicePagamento ?? anagrafica.IdPag,
            IdBancaAppoggio          = request.IdBancaAppoggio ?? anagrafica.IdBancaAppoggio,
            // Snapshot fiscali congelati all'emissione.
            AliquotaIva              = request.AliquotaIva,
            AliquotaCnpaia           = aliquote.Cnpaia,
            AliquotaRitenuta         = aliquote.Ritenuta,
            ApplicaRitenuta          = anagrafica.SostitutoImposta,
            DescrizioneSpeseInAvviso = request.DescrizioneSpeseInAvviso,
            IsAttivo                 = true,
        };

        await _repo.EmettiAsync(testata, righe, request.IdSpeseSelezionate, ct);

        try
        {
            await _audit.RegistraCreazioneAsync(
                "AvvisoFattura", idAvviso,
                DescrizioneAudit(testata),
                AuditDettaglio.Snapshot(new
                {
                    testata.IdAttivita,
                    testata.IdAnagrafica,
                    testata.DataAvviso,
                    NumeroRighe = righe.Count,
                    testata.AliquotaIva,
                    testata.AliquotaCnpaia,
                    testata.AliquotaRitenuta,
                    testata.ApplicaRitenuta,
                }),
                cancellationToken: ct);
        }
        catch { /* audit best-effort */ }

        return idAvviso;
    }

    // -----------------------------------------------------------------------
    // Annullamento (atomico)
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task AnnullaAsync(Guid idAvviso, CancellationToken ct = default)
    {
        var avviso = await _repo.GetByIdAsync(idAvviso, ct);
        if (avviso is null || !avviso.IsAttivo) return; // idempotente

        await _repo.AnnullaAsync(idAvviso, ct);

        try
        {
            await _audit.RegistraEliminazioneAsync(
                "AvvisoFattura", idAvviso,
                DescrizioneAudit(avviso),
                AuditDettaglio.Snapshot(new
                {
                    avviso.IdAttivita,
                    avviso.IdAnagrafica,
                    avviso.DataAvviso,
                }),
                cancellationToken: ct);
        }
        catch { /* audit best-effort */ }
    }

    // -----------------------------------------------------------------------
    // Aggiornamento testata
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task AggiornaTestataAsync(AvvisoFattura avviso, CancellationToken ct = default)
    {
        if (avviso.DataAvviso == default)
            throw new AvvisoFatturaInvalidaException(
                AvvisoFatturaMotivoInvalido.DataObbligatoria,
                "La data dell'avviso è obbligatoria.");

        var prima = await _repo.GetByIdAsync(avviso.IdAvviso, ct);
        await _repo.UpdateAsync(avviso, ct);

        try
        {
            await _audit.RegistraModificaAsync(
                "AvvisoFattura", avviso.IdAvviso,
                DescrizioneAudit(avviso),
                prima is not null ? AuditDettaglio.Diff(prima, avviso) : null,
                cancellationToken: ct);
        }
        catch { /* audit best-effort */ }
    }

    // -----------------------------------------------------------------------
    // Calcolo fiscale (puro, sugli snapshot dell'avviso)
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public CalcoloFiscaleRisultato Calcola(AvvisoFattura avviso, decimal imponibile, decimal speseArt15)
        => _calcolo.Calcola(new CalcoloFiscaleInput(
            Imponibile:       imponibile,
            AliquotaCassa:    avviso.AliquotaCnpaia,
            AliquotaIva:      avviso.AliquotaIva,
            AliquotaRitenuta: avviso.AliquotaRitenuta,
            ApplicaCassa:     true,                    // lo studio applica sempre la cassa
            ApplicaRitenuta:  avviso.ApplicaRitenuta,  // solo clienti sostituti d'imposta
            SpeseArt15:       speseArt15));

    // Etichetta breve leggibile per l'audit (oggetto o data).
    private static string DescrizioneAudit(AvvisoFattura a)
        => !string.IsNullOrWhiteSpace(a.Oggetto)
            ? a.Oggetto!
            : $"Avviso del {a.DataAvviso:dd/MM/yyyy}";
}
