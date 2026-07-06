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
    private readonly IAziendaManager              _azienda;
    private readonly ICalcoloFiscaleAvviso        _calcolo;
    private readonly IAuditManager                _audit;

    public AvvisoFatturaManager(
        IAvvisoFatturaRepository repo,
        IScadenzaPagamentoRepository scadenze,
        IAnagraficaRepository anagrafiche,
        IAliquotaManager aliquote,
        IAziendaManager azienda,
        ICalcoloFiscaleAvviso calcolo,
        IAuditManager audit)
    {
        _repo        = repo;
        _scadenze    = scadenze;
        _anagrafiche = anagrafiche;
        _aliquote    = aliquote;
        _azienda     = azienda;
        _calcolo     = calcolo;
        _audit       = audit;
    }

    // -----------------------------------------------------------------------
    // Letture
    // -----------------------------------------------------------------------

    public Task<IReadOnlyList<AvvisoFattura>> ElencoPerAttivitaAsync(Guid idAttivita, CancellationToken ct = default)
        => _repo.GetByAttivitaAsync(idAttivita, ct);

    public Task<IReadOnlyList<AttivitaFatturabile>> AttivitaConAvvisiNonFatturatiAsync(CancellationToken ct = default)
        => _repo.GetAttivitaConAvvisiNonFatturatiAsync(ct);

    public async Task<AvvisoDettaglio?> GetDettaglioAsync(Guid idAvviso, CancellationToken ct = default)
    {
        var testata = await _repo.GetByIdAsync(idAvviso, ct);
        if (testata is null) return null;
        var righe = await _repo.GetRigheByAvvisoAsync(idAvviso, ct);
        return new AvvisoDettaglio(testata, righe);
    }

    public Task<IReadOnlyList<DettaglioAvvisoGrandezze>> DettagliGrandezzeAsync(Guid idAvviso, CancellationToken ct = default)
        => _repo.GetDettagliGrandezzeByAvvisoAsync(idAvviso, ct);

    public Task<IReadOnlyList<ScadenzaFatturabile>> ScadenzeFatturabiliAsync(Guid idAttivita, Guid? idAvvisoEscluso = null, CancellationToken ct = default)
        => _scadenze.GetFatturabiliByAttivitaAsync(idAttivita, idAvvisoEscluso, ct);

    /// <summary>
    /// Universo delle attività su cui resta lavoro di fatturazione, per i filtri della
    /// maschera avvisi. È l'UNIONE di:
    /// <list type="bullet">
    ///   <item>attività con <b>residuo da fatturare</b> (scadenze non ancora in un avviso)
    ///   → si può creare un nuovo avviso;</item>
    ///   <item>attività con <b>avvisi non ancora fatturati</b> → si deve ancora emettere la
    ///   fattura dall'avviso.</item>
    /// </list>
    /// Così un'attività resta selezionabile finché non è tutto fatturato: senza l'unione,
    /// un avviso che satura tutte le scadenze farebbe sparire l'attività dal filtro
    /// <b>prima</b> di poter creare la fattura da quell'avviso (l'utente resterebbe bloccato).
    /// </summary>
    public async Task<IReadOnlyList<AttivitaFatturabile>> AttivitaFatturabiliAsync(CancellationToken ct = default)
    {
        var conResiduo = await _scadenze.GetAttivitaConResiduoDaFatturareAsync(ct);
        var conAvvisi  = await _repo.GetAttivitaConAvvisiNonFatturatiAsync(ct);
        // AttivitaFatturabile è un record → Distinct deduplica per (IdAnagrafica, IdAttivita).
        return conResiduo.Concat(conAvvisi).Distinct().ToList();
    }

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

        // Un avviso deve avere contenuto reale, ma non necessariamente delle rate:
        // è ammesso l'avviso con SOLE spese anticipate (art. 15) da riaddebitare.
        // Serve quindi almeno una rata da fatturare OPPURE almeno una spesa allegata.
        if (!request.Righe.Any(r => r.IdScadenza is not null) && request.IdSpeseSelezionate.Count == 0)
            throw new AvvisoFatturaInvalidaException(
                AvvisoFatturaMotivoInvalido.NessunaScadenzaSelezionata,
                "Seleziona almeno una rata da fatturare o una spesa da riaddebitare.");

        var anagrafica = await _anagrafiche.GetByIdAsync(request.IdAnagrafica, ct)
            ?? throw new AvvisoFatturaInvalidaException(
                AvvisoFatturaMotivoInvalido.AnagraficaNonTrovata,
                "Cliente non trovato.");

        // Snapshot autorevole delle rate: la fatturabilità (esiste, attiva, non
        // consumata, dell'attività) è garantita dalla presenza in questa mappa.
        var fatturabili = (await _scadenze.GetFatturabiliByAttivitaAsync(request.IdAttivita, ct: ct))
            .ToDictionary(f => f.IdScadenza);

        var aliquote = await _aliquote.GetAliquoteAvvisoAsync(ct);

        // Profilo fiscale del cedente (migration 069): decide se cassa/ritenuta si
        // applicano. Per una S.r.l. commerciale entrambi off → snapshot a 0/false, e
        // avviso/PDF/XML restano "puliti" (imponibile + IVA). Se l'azienda non è
        // configurata, prudenzialmente NON si applicano (documento neutro).
        var azienda = await _azienda.GetAziendaAsync(ct);
        var applicaCassa    = azienda?.ApplicaCassaPrevidenziale ?? false;
        var soggettoRitenuta = azienda?.SoggettoARitenuta ?? false;

        var idAvviso = Guid.CreateVersion7();
        var righe    = new List<AvvisoFatturaRiga>();
        var ordine   = 1;

        // Righe nell'ordine deciso dall'utente: reali (scadenza, importo/etichette
        // autorevoli dal read-model) e descrittive (solo testo) possono alternarsi.
        foreach (var riga in request.Righe)
        {
            if (riga.IdScadenza is { } idScadenza)
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
            else
            {
                righe.Add(new AvvisoFatturaRiga
                {
                    IdRiga        = Guid.CreateVersion7(),
                    IdAvviso      = idAvviso,
                    Ordine        = ordine++,
                    Descrizione   = riga.Descrizione ?? string.Empty,
                    IsDescrittiva = true,
                });
            }
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
            // Snapshot fiscali congelati all'emissione, filtrati dal profilo del cedente.
            // Cassa: solo se il cedente la prevede (altrimenti aliquota 0 → nessuna cassa).
            // Ritenuta: solo se il cedente vi è soggetto E il cliente è sostituto d'imposta.
            AliquotaIva              = request.AliquotaIva,
            AliquotaCnpaia           = applicaCassa ? aliquote.Cnpaia : 0m,
            AliquotaRitenuta         = aliquote.Ritenuta,
            ApplicaRitenuta          = soggettoRitenuta && anagrafica.SostitutoImposta,
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

        // Un avviso già fatturato non è annullabile: si orfanerebbe la fattura e si
        // sbloccherebbero rate che risultano su una fattura. Prima si annulla la fattura.
        if (avviso.IsFatturato)
            throw new AvvisoFatturaInvalidaException(
                AvvisoFatturaMotivoInvalido.AvvisoGiaFatturato,
                "L'avviso è già stato fatturato: annulla prima la fattura per poterlo eliminare.");

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
        if (prima is null || !prima.IsAttivo)
            throw new AvvisoFatturaInvalidaException(
                AvvisoFatturaMotivoInvalido.AvvisoNonTrovato,
                "L'avviso non esiste più o è stato annullato.");
        // Un avviso fatturato è congelato: la fattura legge live testata/righe/snapshot.
        if (prima.IsFatturato)
            throw new AvvisoFatturaInvalidaException(
                AvvisoFatturaMotivoInvalido.AvvisoGiaFatturato,
                "L'avviso è già stato fatturato: annulla prima la fattura per modificarlo.");

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
    // Modifica dettagli (righe) di un avviso esistente (atomica)
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task AggiornaDettagliAsync(
        Guid idAvviso,
        IReadOnlyList<ModificaRigaAvvisoInput> righe,
        IReadOnlyList<Guid> idSpeseSelezionate,
        CancellationToken ct = default)
    {
        var avviso = await _repo.GetByIdAsync(idAvviso, ct);
        if (avviso is null || !avviso.IsAttivo)
            throw new AvvisoFatturaInvalidaException(
                AvvisoFatturaMotivoInvalido.AvvisoNonTrovato,
                "L'avviso non esiste più o è stato annullato.");
        if (avviso.IsFatturato)
            throw new AvvisoFatturaInvalidaException(
                AvvisoFatturaMotivoInvalido.AvvisoGiaFatturato,
                "L'avviso è già stato fatturato: annulla prima la fattura per modificarlo.");

        // Fonti autorevoli per le righe reali:
        //   • rate MANTENUTE → riga corrente dell'avviso (tipo/importo/dettaglio);
        //   • rate AGGIUNTE  → read-model fatturabili dell'attività, escludendo questo
        //     avviso (le sue rate sono nella bozza, non nel pool disponibile).
        var correnti = (await _repo.GetRigheByAvvisoAsync(idAvviso, ct))
            .Where(r => !r.IsDescrittiva && r.IdScadenza is not null)
            .ToDictionary(r => r.IdScadenza!.Value);
        var fatturabili = (await _scadenze.GetFatturabiliByAttivitaAsync(avviso.IdAttivita, avviso.IdAvviso, ct))
            .ToDictionary(f => f.IdScadenza);

        var nuove  = new List<AvvisoFatturaRiga>();
        var ordine = 1;
        foreach (var input in righe)
        {
            if (input.IdScadenza is { } idScad)
            {
                if (correnti.TryGetValue(idScad, out var orig))
                {
                    nuove.Add(new AvvisoFatturaRiga
                    {
                        IdRiga              = Guid.CreateVersion7(),
                        IdAvviso            = idAvviso,
                        Ordine              = ordine++,
                        IdAttivitaDettaglio = orig.IdAttivitaDettaglio,
                        IdScadenza          = orig.IdScadenza,
                        Tipo                = orig.Tipo,
                        // Importo e tipo restano autorevoli; la descrizione è editabile.
                        Descrizione         = string.IsNullOrWhiteSpace(input.Descrizione) ? orig.Descrizione : input.Descrizione.Trim(),
                        Importo             = orig.Importo,
                        IsDescrittiva       = false,
                    });
                }
                else if (fatturabili.TryGetValue(idScad, out var f))
                {
                    nuove.Add(new AvvisoFatturaRiga
                    {
                        IdRiga              = Guid.CreateVersion7(),
                        IdAvviso            = idAvviso,
                        Ordine              = ordine++,
                        IdAttivitaDettaglio = f.IdAttivitaDettaglio,
                        IdScadenza          = f.IdScadenza,
                        Tipo                = f.TipoDettaglioDescrizione,
                        Descrizione         = string.IsNullOrWhiteSpace(input.Descrizione) ? f.DescrizioneDettaglio : input.Descrizione.Trim(),
                        Importo             = f.Importo,
                        IsDescrittiva       = false,
                    });
                }
                else
                {
                    throw new AvvisoFatturaInvalidaException(
                        AvvisoFatturaMotivoInvalido.ScadenzaNonFatturabile,
                        "Una rata selezionata non è più disponibile: ricarica l'elenco delle scadenze.");
                }
            }
            else
            {
                nuove.Add(new AvvisoFatturaRiga
                {
                    IdRiga        = Guid.CreateVersion7(),
                    IdAvviso      = idAvviso,
                    Ordine        = ordine++,
                    Descrizione   = input.Descrizione?.Trim() ?? string.Empty,
                    IsDescrittiva = true,
                });
            }
        }

        // Un avviso deve avere contenuto reale: almeno una rata OPPURE una spesa
        // (stessa regola dell'emissione: è ammesso l'avviso di sole spese art. 15).
        var realiNuove = nuove.Count(r => !r.IsDescrittiva);
        if (realiNuove == 0 && idSpeseSelezionate.Count == 0)
            throw new AvvisoFatturaInvalidaException(
                AvvisoFatturaMotivoInvalido.NessunaScadenzaSelezionata,
                "L'avviso deve avere almeno una rata o una spesa. Per svuotarlo del tutto, annullalo.");

        await _repo.AggiornaRigheAsync(idAvviso, nuove, idSpeseSelezionate, ct);

        try
        {
            await _audit.RegistraModificaAsync(
                "AvvisoFattura", idAvviso,
                DescrizioneAudit(avviso),
                AuditDettaglio.Snapshot(new
                {
                    RigheReali       = realiNuove,
                    RigheDescrittive = nuove.Count - realiNuove,
                    Spese            = idSpeseSelezionate.Count,
                }),
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
            // Cassa/ritenuta guidate dallo snapshot dell'avviso (già filtrato dal profilo
            // del cedente all'emissione): aliquota cassa 0 → nessuna cassa.
            ApplicaCassa:     avviso.AliquotaCnpaia > 0m,
            ApplicaRitenuta:  avviso.ApplicaRitenuta,
            SpeseArt15:       speseArt15));

    // Etichetta breve leggibile per l'audit (oggetto o data).
    private static string DescrizioneAudit(AvvisoFattura a)
        => !string.IsNullOrWhiteSpace(a.Oggetto)
            ? a.Oggetto!
            : $"Avviso del {a.DataAvviso:dd/MM/yyyy}";
}
