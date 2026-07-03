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

    // Etichetta breve leggibile per l'audit: "Fattura 38/2026".
    private static string DescrizioneAudit(Fattura f) => $"Fattura {f.NumeroFattura}/{f.Anno}";
}
