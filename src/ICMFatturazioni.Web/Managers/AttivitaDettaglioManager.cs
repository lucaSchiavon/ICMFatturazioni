using ICMFatturazioni.Web.Auditing;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Implementazione di <see cref="IAttivitaDettaglioManager"/>.
/// Regole business:
///   • Ordine assegnato dal Manager (max attivo + 1).
///   • Alla creazione, il dettaglio nasce già con una scadenza di default che copre
///     l'intero importo (data = termine previsto): l'utente trova la ripartizione già
///     completa e può poi frazionarla. Delegata a <see cref="IScadenzaPagamentoManager"/>.
///   • HasFattura = true blocca modifiche ed eliminazione.
///   • Sposta Su/Giù: carica la lista e scambia tramite Repository.
///   • Audit best-effort su ogni scrittura.
/// </summary>
public sealed class AttivitaDettaglioManager : IAttivitaDettaglioManager
{
    private readonly IAttivitaDettaglioRepository _repo;
    private readonly IScadenzaPagamentoManager    _scadenze;
    private readonly IAuditManager                _audit;

    public AttivitaDettaglioManager(
        IAttivitaDettaglioRepository repo,
        IScadenzaPagamentoManager    scadenze,
        IAuditManager                audit)
    {
        _repo     = repo;
        _scadenze = scadenze;
        _audit    = audit;
    }

    public Task<IReadOnlyList<AttivitaDettaglio>> ElencoPerAttivitaAsync(Guid idAttivita, CancellationToken ct = default)
        => _repo.GetByAttivitaAsync(idAttivita, ct);

    /// <inheritdoc/>
    public async Task<Guid> CreaAsync(AttivitaDettaglio dettaglio, CancellationToken ct = default)
    {
        ValidaCampi(dettaglio);
        dettaglio.IdAttivitaDettaglio = Guid.CreateVersion7();
        dettaglio.Ordine = (await _repo.GetMaxOrdineAsync(dettaglio.IdAttivita, ct)) + 1;
        await _repo.InsertAsync(dettaglio, ct);

        try
        {
            await _audit.RegistraCreazioneAsync(
                "AttivitaDettaglio", dettaglio.IdAttivitaDettaglio,
                dettaglio.DescrizioneDettaglio,
                AuditDettaglio.Snapshot(new
                {
                    dettaglio.IdAttivita, dettaglio.IdTipoDettaglioAttivita,
                    dettaglio.Ordine, dettaglio.DescrizioneDettaglio,
                    dettaglio.Importo, dettaglio.NotaDettaglio, dettaglio.TerminePrevisto,
                }),
                cancellationToken: ct);
        }
        catch { /* audit best-effort */ }

        // Scadenza di default: copre l'intero importo del dettaglio con data = termine
        // previsto (obbligatorio, quindi sempre valorizzato). Così il dettaglio nasce con
        // la ripartizione già completa; l'utente potrà poi frazionarla in Gestione Scadenze.
        // La creazione riusa la validazione e l'audit di ScadenzaPagamentoManager.
        await _scadenze.CreaAsync(new ScadenzaPagamento
        {
            IdAttivitaDettaglio = dettaglio.IdAttivitaDettaglio,
            DataScadenza        = dettaglio.TerminePrevisto!.Value,
            Importo             = dettaglio.Importo,
        }, ct);

        return dettaglio.IdAttivitaDettaglio;
    }

    /// <inheritdoc/>
    public async Task AggiornaAsync(AttivitaDettaglio dettaglio, CancellationToken ct = default)
    {
        if (dettaglio.HasFattura)
            throw new AttivitaDettaglioInvalidaException(
                AttivitaDettaglioMotivoInvalido.HasFattura,
                "La riga è collegata a una fattura emessa e non può essere modificata.");

        ValidaCampi(dettaglio);

        var prima = await _repo.GetByIdAsync(dettaglio.IdAttivitaDettaglio, ct);
        await _repo.UpdateAsync(dettaglio, ct);

        try
        {
            await _audit.RegistraModificaAsync(
                "AttivitaDettaglio", dettaglio.IdAttivitaDettaglio,
                dettaglio.DescrizioneDettaglio,
                prima is not null ? AuditDettaglio.Diff(prima, dettaglio) : null,
                cancellationToken: ct);
        }
        catch { /* audit best-effort */ }
    }

    /// <inheritdoc/>
    public async Task EliminaAsync(Guid idAttivitaDettaglio, CancellationToken ct = default)
    {
        var dettaglio = await _repo.GetByIdAsync(idAttivitaDettaglio, ct);
        if (dettaglio is null) return;

        if (dettaglio.HasFattura)
            throw new AttivitaDettaglioInvalidaException(
                AttivitaDettaglioMotivoInvalido.HasFattura,
                "La riga è collegata a una fattura emessa e non può essere eliminata.");

        await _repo.DisattivaAsync(idAttivitaDettaglio, ct);

        try
        {
            await _audit.RegistraEliminazioneAsync(
                "AttivitaDettaglio", idAttivitaDettaglio,
                dettaglio.DescrizioneDettaglio,
                AuditDettaglio.Snapshot(new
                {
                    dettaglio.IdAttivita, dettaglio.DescrizioneDettaglio,
                    dettaglio.Importo, dettaglio.Ordine,
                }),
                cancellationToken: ct);
        }
        catch { /* audit best-effort */ }
    }

    /// <inheritdoc/>
    public async Task SpostaSuAsync(Guid idAttivitaDettaglio, CancellationToken ct = default)
    {
        var corrente = await _repo.GetByIdAsync(idAttivitaDettaglio, ct);
        if (corrente is null) return;

        var lista = (await _repo.GetByAttivitaAsync(corrente.IdAttivita, ct)).ToList();
        var idx   = lista.FindIndex(d => d.IdAttivitaDettaglio == idAttivitaDettaglio);
        if (idx <= 0) return; // già primo

        var precedente = lista[idx - 1];
        await _repo.ScambiaOrdineAsync(
            corrente.IdAttivitaDettaglio, precedente.IdAttivitaDettaglio,
            corrente.Ordine, precedente.Ordine, ct);
    }

    /// <inheritdoc/>
    public async Task SpostaGiuAsync(Guid idAttivitaDettaglio, CancellationToken ct = default)
    {
        var corrente = await _repo.GetByIdAsync(idAttivitaDettaglio, ct);
        if (corrente is null) return;

        var lista = (await _repo.GetByAttivitaAsync(corrente.IdAttivita, ct)).ToList();
        var idx   = lista.FindIndex(d => d.IdAttivitaDettaglio == idAttivitaDettaglio);
        if (idx < 0 || idx >= lista.Count - 1) return; // già ultimo

        var successivo = lista[idx + 1];
        await _repo.ScambiaOrdineAsync(
            corrente.IdAttivitaDettaglio, successivo.IdAttivitaDettaglio,
            corrente.Ordine, successivo.Ordine, ct);
    }

    private static void ValidaCampi(AttivitaDettaglio d)
    {
        if (d.IdTipoDettaglioAttivita == Guid.Empty)
            throw new AttivitaDettaglioInvalidaException(
                AttivitaDettaglioMotivoInvalido.TipoDettaglioObbligatorio,
                "Selezionare il tipo dettaglio.");

        if (string.IsNullOrWhiteSpace(d.DescrizioneDettaglio))
            throw new AttivitaDettaglioInvalidaException(
                AttivitaDettaglioMotivoInvalido.DescrizioneObbligatoria,
                "La descrizione del dettaglio è obbligatoria.");

        if (d.TerminePrevisto is null)
            throw new AttivitaDettaglioInvalidaException(
                AttivitaDettaglioMotivoInvalido.TerminePrevistoObbligatorio,
                "Il termine previsto è obbligatorio.");

        if (d.Importo <= 0)
            throw new AttivitaDettaglioInvalidaException(
                AttivitaDettaglioMotivoInvalido.ImportoNonValido,
                "L'importo deve essere maggiore di zero.");
    }
}
