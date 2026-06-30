using ICMFatturazioni.Web.Auditing;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Implementazione di <see cref="IScadenzaPagamentoManager"/>.
/// Dipende da <see cref="IAttivitaDettaglioRepository"/> per verificare
/// <c>HasFattura</c> del dettaglio parent prima di ogni modifica/eliminazione.
/// </summary>
public sealed class ScadenzaPagamentoManager : IScadenzaPagamentoManager
{
    private readonly IScadenzaPagamentoRepository  _repo;
    private readonly IAttivitaDettaglioRepository  _repoDettaglio;
    private readonly IAuditManager                 _audit;

    public ScadenzaPagamentoManager(
        IScadenzaPagamentoRepository repo,
        IAttivitaDettaglioRepository repoDettaglio,
        IAuditManager                audit)
    {
        _repo          = repo;
        _repoDettaglio = repoDettaglio;
        _audit         = audit;
    }

    public Task<IReadOnlyList<ScadenzaPagamento>> ElencoPerDettaglioAsync(Guid idAttivitaDettaglio, CancellationToken ct = default)
        => _repo.GetByDettaglioAsync(idAttivitaDettaglio, ct);

    /// <inheritdoc/>
    public async Task<Guid> CreaAsync(ScadenzaPagamento scadenza, CancellationToken ct = default)
    {
        ValidaCampi(scadenza);

        // Controllo HasFattura del dettaglio parent.
        var dettaglio = await _repoDettaglio.GetByIdAsync(scadenza.IdAttivitaDettaglio, ct);
        if (dettaglio is { HasFattura: true })
            throw new ScadenzaPagamentoInvalidaException(
                ScadenzaPagamentoMotivoInvalido.DettaglioFatturato,
                "La riga è collegata a una fattura emessa: non è possibile aggiungere scadenze.");

        // Sentinel: la somma delle scadenze attive non può eccedere l'importo del
        // dettaglio (le scadenze sono una ripartizione completa dell'importo).
        await ValidaSommaNonEccedenteAsync(scadenza, dettaglio, escludiIdScadenza: null, ct);

        scadenza.IdScadenza = Guid.CreateVersion7();
        await _repo.InsertAsync(scadenza, ct);

        try
        {
            await _audit.RegistraCreazioneAsync(
                "ScadenzaPagamento", scadenza.IdScadenza,
                scadenza.DataScadenza.ToString("dd/MM/yyyy"),
                AuditDettaglio.Snapshot(new
                {
                    scadenza.IdAttivitaDettaglio,
                    scadenza.DataScadenza,
                    scadenza.Importo,
                    scadenza.Nota,
                }),
                cancellationToken: ct);
        }
        catch { /* audit best-effort */ }

        return scadenza.IdScadenza;
    }

    /// <inheritdoc/>
    public async Task AggiornaAsync(ScadenzaPagamento scadenza, CancellationToken ct = default)
    {
        ValidaCampi(scadenza);

        var dettaglio = await _repoDettaglio.GetByIdAsync(scadenza.IdAttivitaDettaglio, ct);
        if (dettaglio is { HasFattura: true })
            throw new ScadenzaPagamentoInvalidaException(
                ScadenzaPagamentoMotivoInvalido.DettaglioFatturato,
                "La riga è collegata a una fattura emessa: non è possibile modificare le scadenze.");

        // Sentinel: la nuova somma (escludendo la scadenza che si sta aggiornando)
        // non può eccedere l'importo del dettaglio.
        await ValidaSommaNonEccedenteAsync(scadenza, dettaglio, escludiIdScadenza: scadenza.IdScadenza, ct);

        var prima = await _repo.GetByIdAsync(scadenza.IdScadenza, ct);
        await _repo.UpdateAsync(scadenza, ct);

        try
        {
            await _audit.RegistraModificaAsync(
                "ScadenzaPagamento", scadenza.IdScadenza,
                scadenza.DataScadenza.ToString("dd/MM/yyyy"),
                prima is not null ? AuditDettaglio.Diff(prima, scadenza) : null,
                cancellationToken: ct);
        }
        catch { /* audit best-effort */ }
    }

    /// <inheritdoc/>
    public async Task EliminaAsync(Guid idScadenza, CancellationToken ct = default)
    {
        var scadenza = await _repo.GetByIdAsync(idScadenza, ct);
        if (scadenza is null) return;

        var dettaglio = await _repoDettaglio.GetByIdAsync(scadenza.IdAttivitaDettaglio, ct);
        if (dettaglio is { HasFattura: true })
            throw new ScadenzaPagamentoInvalidaException(
                ScadenzaPagamentoMotivoInvalido.DettaglioFatturato,
                "La riga è collegata a una fattura emessa: non è possibile eliminare le scadenze.");

        await _repo.DisattivaAsync(idScadenza, ct);

        try
        {
            await _audit.RegistraEliminazioneAsync(
                "ScadenzaPagamento", idScadenza,
                scadenza.DataScadenza.ToString("dd/MM/yyyy"),
                AuditDettaglio.Snapshot(new
                {
                    scadenza.IdAttivitaDettaglio,
                    scadenza.DataScadenza,
                    scadenza.Importo,
                    scadenza.Nota,
                }),
                cancellationToken: ct);
        }
        catch { /* audit best-effort */ }
    }

    /// <summary>
    /// Sentinel di correttezza: la somma delle scadenze attive del dettaglio
    /// (più la scadenza in inserimento/modifica) non può superare l'importo del
    /// dettaglio. Le scadenze sono una ripartizione completa dell'importo: il
    /// limite superiore è invariante e va difeso a prescindere dalla UI.
    /// </summary>
    private async Task ValidaSommaNonEccedenteAsync(
        ScadenzaPagamento scadenza,
        AttivitaDettaglio? dettaglio,
        Guid? escludiIdScadenza,
        CancellationToken ct)
    {
        // Senza dettaglio non si conosce l'importo di riferimento: nulla da validare.
        if (dettaglio is null) return;

        var esistenti = await _repo.GetByDettaglioAsync(scadenza.IdAttivitaDettaglio, ct);
        var sommaAltre = esistenti
            .Where(s => escludiIdScadenza is null || s.IdScadenza != escludiIdScadenza.Value)
            .Sum(s => s.Importo);

        // Tolleranza per arrotondamenti su DECIMAL(18,2).
        if (sommaAltre + scadenza.Importo > dettaglio.Importo + 0.005m)
            throw new ScadenzaPagamentoInvalidaException(
                ScadenzaPagamentoMotivoInvalido.SommaEccedeImporto,
                $"La somma delle scadenze ({(sommaAltre + scadenza.Importo).ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("it-IT"))} €) " +
                $"supererebbe l'importo del dettaglio ({dettaglio.Importo.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("it-IT"))} €).");
    }

    private static void ValidaCampi(ScadenzaPagamento s)
    {
        if (s.DataScadenza == default)
            throw new ScadenzaPagamentoInvalidaException(
                ScadenzaPagamentoMotivoInvalido.DataObbligatoria,
                "La data di scadenza è obbligatoria.");

        if (s.Importo <= 0)
            throw new ScadenzaPagamentoInvalidaException(
                ScadenzaPagamentoMotivoInvalido.ImportoNonValido,
                "L'importo deve essere maggiore di zero.");
    }
}
