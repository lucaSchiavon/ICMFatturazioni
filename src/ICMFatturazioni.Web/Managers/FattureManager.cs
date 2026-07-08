using ICMFatturazioni.Web.Auditing;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Models;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Implementazione di <see cref="IFattureManager"/>.
///
/// Responsabilità: validazione del comando, numerazione progressiva per anno
/// solare, guardia anti doppia-fatturazione (l'avviso deve esistere, essere attivo
/// e non ancora fatturato) e audit best-effort. L'atomicità qui è semplice (una
/// tabella): non serve aggregate write come per l'avviso.
/// </summary>
public sealed class FattureManager : IFattureManager
{
    private readonly IFattureRepository       _repo;
    private readonly IAvvisoFatturaRepository _avvisi;
    private readonly IAuditManager            _audit;

    public FattureManager(
        IFattureRepository repo,
        IAvvisoFatturaRepository avvisi,
        IAuditManager audit)
    {
        _repo   = repo;
        _avvisi = avvisi;
        _audit  = audit;
    }

    public Task<Fattura?> GetByIdAsync(Guid idFattura, CancellationToken ct = default)
        => _repo.GetByIdAsync(idFattura, ct);

    public Task<IReadOnlyList<FatturaEmessa>> ElencoEmessePerAttivitaAsync(Guid idAttivita, CancellationToken ct = default)
        => _repo.GetEmesseByAttivitaAsync(idAttivita, ct);

    public Task<IReadOnlyList<int>> AnniConFattureAsync(CancellationToken ct = default)
        => _repo.GetAnniConFattureAsync(ct);

    public Task<IReadOnlyList<AttivitaFatturabile>> AttivitaConFattureAsync(CancellationToken ct = default)
        => _repo.GetAttivitaConFattureAsync(ct);

    public Task<Fattura?> GetAttivaByAvvisoAsync(Guid idAvviso, CancellationToken ct = default)
        => _repo.GetAttivaByAvvisoAsync(idAvviso, ct);

    public async Task<int> ProponiNumeroAsync(int anno, CancellationToken ct = default)
        => await _repo.GetMaxNumeroAnnoAsync(anno, ct) + 1;

    /// <inheritdoc/>
    public async Task<Guid> CreaAsync(CreaFatturaRequest request, CancellationToken ct = default)
    {
        if (request.DataFattura == default)
            throw new FatturaInvalidaException(
                FatturaMotivoInvalido.DataObbligatoria,
                "La data della fattura è obbligatoria.");

        if (request.NumeroFattura <= 0)
            throw new FatturaInvalidaException(
                FatturaMotivoInvalido.NumeroNonValido,
                "Il numero della fattura deve essere maggiore di zero.");

        // L'avviso deve esistere ed essere attivo (pre-check UX; il DB è comunque
        // difeso dall'indice univoco filtrato su IdAvviso).
        var avviso = await _avvisi.GetByIdAsync(request.IdAvviso, ct);
        if (avviso is null || !avviso.IsAttivo)
            throw new FatturaInvalidaException(
                FatturaMotivoInvalido.AvvisoNonTrovato,
                "L'avviso da fatturare non esiste più o è stato annullato.");

        // Pre-check "già fatturato": messaggio specifico. Il sentinel resta l'indice
        // univoco (UQ_Fatture_IdAvviso_Attiva), che copre anche la race condition.
        var esistente = await _repo.GetAttivaByAvvisoAsync(request.IdAvviso, ct);
        if (esistente is not null)
            throw new FatturaInvalidaException(
                FatturaMotivoInvalido.AvvisoGiaFatturato,
                "Questo avviso è già stato fatturato.");

        var fattura = new Fattura
        {
            IdFattura     = Guid.CreateVersion7(),
            IdAvviso      = request.IdAvviso,
            NumeroFattura = request.NumeroFattura,
            Anno          = request.DataFattura.Year,
            DataFattura   = request.DataFattura,
            CreatoXML     = false,
            EsitoXML      = 0,
            // CIG/CUP dell'appalto pubblico: normalizzati (vuoto → null), così una
            // fattura verso privati non porta stringhe vuote nel tracciato.
            Cig           = NullSeVuoto(request.Cig),
            Cup           = NullSeVuoto(request.Cup),
            IsAttivo      = true,
        };

        await _repo.CreateAsync(fattura, ct);

        try
        {
            await _audit.RegistraCreazioneAsync(
                "Fattura", fattura.IdFattura,
                DescrizioneAudit(fattura),
                AuditDettaglio.Snapshot(new
                {
                    fattura.IdAvviso,
                    fattura.NumeroFattura,
                    fattura.Anno,
                    fattura.DataFattura,
                    fattura.Cig,
                    fattura.Cup,
                }),
                cancellationToken: ct);
        }
        catch { /* audit best-effort */ }

        return fattura.IdFattura;
    }

    /// <inheritdoc/>
    public async Task AnnullaAsync(Guid idFattura, CancellationToken ct = default)
    {
        var fattura = await _repo.GetByIdAsync(idFattura, ct);
        if (fattura is null || !fattura.IsAttivo) return; // idempotente

        // Doppia difesa (TOCTOU): una fattura con tracciato XML NON si elimina
        // direttamente. Prima va rimosso l'XML (e solo se non ha esito OK), poi la
        // fattura — ordine simmetrico alla creazione. Il pre-check dà il messaggio
        // UX; il sentinel `AND CreatoXML = 0` nell'UPDATE copre la race condition.
        if (fattura.CreatoXML)
            throw new FatturaInvalidaException(
                FatturaMotivoInvalido.FatturaConXmlNonEliminabile,
                "La fattura ha un tracciato XML: elimina prima l'XML dalla maschera " +
                "Documenti XML, poi potrai eliminare la fattura.");

        await _repo.AnnullaAsync(idFattura, ct);

        try
        {
            await _audit.RegistraEliminazioneAsync(
                "Fattura", idFattura,
                DescrizioneAudit(fattura),
                AuditDettaglio.Snapshot(new
                {
                    fattura.IdAvviso,
                    fattura.NumeroFattura,
                    fattura.Anno,
                    fattura.DataFattura,
                }),
                cancellationToken: ct);
        }
        catch { /* audit best-effort */ }
    }

    // ── Fase D1 — maschera "Creazione-Gestione XML Documenti" ─────────────────

    public Task<IReadOnlyList<DocumentoXmlRiga>> ElencoPerXmlAsync(FiltroDocumentiXml filtro, CancellationToken ct = default)
        => _repo.GetPerXmlAsync(filtro, ct);

    public Task<long> ProssimoProgressivoInvioSeqAsync(CancellationToken ct = default)
        => _repo.GetNextProgressivoInvioAsync(ct);

    /// <inheritdoc/>
    public async Task SegnaXmlCreatoAsync(Guid idFattura, string progressivoInvio, string nomeFileXml, CancellationToken ct = default)
    {
        var fattura = await _repo.GetByIdAsync(idFattura, ct);
        if (fattura is null || !fattura.IsAttivo)
            throw new FatturaInvalidaException(
                FatturaMotivoInvalido.FatturaNonTrovata,
                "La fattura non esiste più o è stata annullata.");

        var giaCreato = fattura.CreatoXML; // per distinguere prima creazione da rigenerazione nell'audit
        await _repo.SetXmlCreatoAsync(idFattura, progressivoInvio, nomeFileXml, DateTime.UtcNow, ct);

        try
        {
            await _audit.RegistraModificaAsync(
                "Fattura", idFattura,
                DescrizioneAudit(fattura),
                AuditDettaglio.Snapshot(new
                {
                    Operazione = giaCreato ? "RigeneraXML" : "CreaXML",
                    ProgressivoInvio = progressivoInvio,
                    NomeFileXml = nomeFileXml,
                }),
                cancellationToken: ct);
        }
        catch { /* audit best-effort */ }
    }

    /// <inheritdoc/>
    public async Task ConfermaEsitoXmlAsync(Guid idFattura, CancellationToken ct = default)
    {
        var fattura = await _repo.GetByIdAsync(idFattura, ct);
        if (fattura is null || !fattura.IsAttivo)
            throw new FatturaInvalidaException(
                FatturaMotivoInvalido.FatturaNonTrovata,
                "La fattura non esiste più o è stata annullata.");

        // Non ha senso confermare l'esito dell'invio se il tracciato XML non esiste.
        if (!fattura.CreatoXML)
            throw new FatturaInvalidaException(
                FatturaMotivoInvalido.XmlNonCreato,
                "Genera prima il file XML: l'esito si conferma solo dopo l'invio allo SdI.");

        await _repo.ConfermaEsitoAsync(idFattura, DateTime.UtcNow, ct);

        try
        {
            await _audit.RegistraModificaAsync(
                "Fattura", idFattura, DescrizioneAudit(fattura),
                AuditDettaglio.Snapshot(new { Operazione = "ConfermaEsitoXML" }),
                cancellationToken: ct);
        }
        catch { /* audit best-effort */ }
    }

    /// <inheritdoc/>
    public async Task TogliEsitoXmlAsync(Guid idFattura, CancellationToken ct = default)
    {
        var fattura = await _repo.GetByIdAsync(idFattura, ct);
        if (fattura is null || !fattura.IsAttivo)
            throw new FatturaInvalidaException(
                FatturaMotivoInvalido.FatturaNonTrovata,
                "La fattura non esiste più o è stata annullata.");

        await _repo.TogliEsitoAsync(idFattura, ct);

        try
        {
            await _audit.RegistraModificaAsync(
                "Fattura", idFattura, DescrizioneAudit(fattura),
                AuditDettaglio.Snapshot(new { Operazione = "TogliEsitoXML" }),
                cancellationToken: ct);
        }
        catch { /* audit best-effort */ }
    }

    /// <inheritdoc/>
    public async Task ResetXmlAsync(Guid idFattura, CancellationToken ct = default)
    {
        var fattura = await _repo.GetByIdAsync(idFattura, ct);
        if (fattura is null || !fattura.IsAttivo)
            throw new FatturaInvalidaException(
                FatturaMotivoInvalido.FatturaNonTrovata,
                "La fattura non esiste più o è stata annullata.");

        // Niente XML da eliminare: idempotente.
        if (!fattura.CreatoXML) return;

        // Blocco simmetrico a quello dell'eliminazione fattura: un XML con esito OK
        // (segnato come inviato allo SdI) non si elimina. Prima si toglie l'esito.
        // Pre-check UX + sentinel `AND EsitoXML = 0` nell'UPDATE (doppia difesa).
        if (fattura.EsitoXML == 1)
            throw new FatturaInvalidaException(
                FatturaMotivoInvalido.XmlConEsitoConfermato,
                "L'XML risulta con esito OK (segnato come inviato allo SdI): togli prima " +
                "l'esito, poi potrai eliminare l'XML.");

        await _repo.ResetXmlAsync(idFattura, ct);

        try
        {
            await _audit.RegistraModificaAsync(
                "Fattura", idFattura, DescrizioneAudit(fattura),
                AuditDettaglio.Snapshot(new
                {
                    Operazione = "EliminaXML",
                    ProgressivoInvioRimosso = fattura.ProgressivoInvio,
                    NomeFileXmlRimosso = fattura.NomeFileXml,
                }),
                cancellationToken: ct);
        }
        catch { /* audit best-effort */ }
    }

    // Etichetta breve leggibile per l'audit: "Fattura 38/2026".
    private static string DescrizioneAudit(Fattura f) => $"Fattura {f.NumeroFattura}/{f.Anno}";

    private static string? NullSeVuoto(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
