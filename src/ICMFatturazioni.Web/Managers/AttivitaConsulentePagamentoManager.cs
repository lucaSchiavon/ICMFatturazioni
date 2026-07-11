using ICMFatturazioni.Web.Auditing;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Models;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Implementazione di <see cref="IAttivitaConsulentePagamentoManager"/>:
///   1) normalizzazione (trim della nota, vuoto → null),
///   2) validazione (riga obbligatoria ed esistente, importo positivo),
///   3) guardia D-C3: la tranche non può superare il residuo della riga —
///      né in creazione né in modifica (in modifica il pagato è ricalcolato
///      escludendo la tranche stessa); pagamenti solo su righe a carico Studio,
///   4) audit di ogni scrittura (Regola 7).
/// </summary>
internal sealed class AttivitaConsulentePagamentoManager : IAttivitaConsulentePagamentoManager
{
    private const string EntityType = nameof(AttivitaConsulentePagamento);

    private readonly IAttivitaConsulentePagamentoRepository _repository;
    private readonly IAuditManager _audit;

    public AttivitaConsulentePagamentoManager(IAttivitaConsulentePagamentoRepository repository, IAuditManager audit)
    {
        _repository = repository;
        _audit = audit;
    }

    public Task<IReadOnlyList<ConsulenzaConSaldo>> ConsulenzeConSaldoAsync(Guid idAttivita, CancellationToken cancellationToken = default)
        => _repository.GetConsulenzeConSaldoAsync(idAttivita, cancellationToken);

    public Task<IReadOnlyList<AttivitaConsulentePagamento>> ElencoPerRigaAsync(Guid idAttivitaConsulente, CancellationToken cancellationToken = default)
        => _repository.GetByRigaAsync(idAttivitaConsulente, cancellationToken);

    public async Task<Guid> CreaAsync(AttivitaConsulentePagamento pagamento, CancellationToken cancellationToken = default)
    {
        var norm = Normalizza(pagamento);
        ValidaCampi(norm);
        await ValidaResiduoAsync(norm, escludiPagamento: null, cancellationToken);

        norm.IdConsulentePagamento = Guid.CreateVersion7();
        await _repository.InsertAsync(norm, cancellationToken);
        await _audit.RegistraCreazioneAsync(EntityType, norm.IdConsulentePagamento,
            DescrizioneAudit(norm), AuditDettaglio.Snapshot(SnapshotAudit(norm)), cancellationToken);
        return norm.IdConsulentePagamento;
    }

    public async Task AggiornaAsync(AttivitaConsulentePagamento pagamento, CancellationToken cancellationToken = default)
    {
        var norm = Normalizza(pagamento);
        ValidaCampi(norm);
        // D-C3 in modifica: il pagato è ricalcolato ESCLUDENDO questa tranche,
        // così il confronto avviene sul residuo che resterebbe senza di lei.
        await ValidaResiduoAsync(norm, escludiPagamento: norm.IdConsulentePagamento, cancellationToken);

        var precedente = await _repository.GetByIdAsync(norm.IdConsulentePagamento, cancellationToken);
        await _repository.UpdateAsync(norm, cancellationToken);
        var dati = precedente is null
            ? AuditDettaglio.Snapshot(SnapshotAudit(norm))
            : AuditDettaglio.Diff(SnapshotAudit(precedente), SnapshotAudit(norm));
        await _audit.RegistraModificaAsync(EntityType, norm.IdConsulentePagamento,
            DescrizioneAudit(norm), dati, cancellationToken);
    }

    public async Task EliminaAsync(Guid idConsulentePagamento, CancellationToken cancellationToken = default)
    {
        // Nessuna guardia: eliminare una tranche fa solo risalire il residuo.
        var pagamento = await _repository.GetByIdAsync(idConsulentePagamento, cancellationToken);
        await _repository.DisattivaAsync(idConsulentePagamento, cancellationToken);
        var dati = pagamento is null ? null : AuditDettaglio.Snapshot(SnapshotAudit(pagamento));
        await _audit.RegistraEliminazioneAsync(EntityType, idConsulentePagamento,
            pagamento is null ? null : DescrizioneAudit(pagamento), dati, cancellationToken);
    }

    // -------------------------------------------------------------------------

    private static AttivitaConsulentePagamento Normalizza(AttivitaConsulentePagamento p) => new()
    {
        IdConsulentePagamento = p.IdConsulentePagamento,
        IdAttivitaConsulente  = p.IdAttivitaConsulente,
        DataPagamento         = p.DataPagamento,
        Importo               = p.Importo,
        Nota                  = string.IsNullOrWhiteSpace(p.Nota) ? null : p.Nota.Trim(),
        IsAttivo              = p.IsAttivo,
    };

    private static void ValidaCampi(AttivitaConsulentePagamento p)
    {
        if (p.IdAttivitaConsulente == Guid.Empty)
            throw new AttivitaConsulentePagamentoInvalidoException(
                AttivitaConsulentePagamentoInvalidoMotivo.RigaObbligatoria,
                "Selezionare una consulenza da saldare.");

        if (p.Importo <= 0)
            throw new AttivitaConsulentePagamentoInvalidoException(
                AttivitaConsulentePagamentoInvalidoMotivo.ImportoNonPositivo,
                "L'importo della tranche deve essere maggiore di zero.");
    }

    // Guardia D-C3: pagamenti solo su righe attive a carico Studio, e mai oltre
    // il residuo (Importo riga − pagato attivo, eventualmente esclusa la tranche
    // in modifica). Il residuo non può mai diventare negativo.
    private async Task ValidaResiduoAsync(AttivitaConsulentePagamento p, Guid? escludiPagamento, CancellationToken ct)
    {
        var saldo = await _repository.GetSaldoRigaAsync(p.IdAttivitaConsulente, escludiPagamento, ct);

        if (saldo is null)
            throw new AttivitaConsulentePagamentoInvalidoException(
                AttivitaConsulentePagamentoInvalidoMotivo.RigaNonTrovata,
                "La consulenza da saldare non esiste o è stata eliminata.");

        if (!saldo.CaricoStudio)
            throw new AttivitaConsulentePagamentoInvalidoException(
                AttivitaConsulentePagamentoInvalidoMotivo.RigaNonACaricoStudio,
                "I pagamenti si registrano solo sulle consulenze a carico dello Studio.");

        var residuo = saldo.Importo - saldo.Pagato;
        if (p.Importo > residuo)
            throw new AttivitaConsulentePagamentoInvalidoException(
                AttivitaConsulentePagamentoInvalidoMotivo.ImportoOltreResiduo,
                $"La tranche ({p.Importo:N2} €) supera il residuo della consulenza ({residuo:N2} €).");
    }

    private static string DescrizioneAudit(AttivitaConsulentePagamento p)
        => $"Tranche {p.Importo:N2} € del {p.DataPagamento:dd/MM/yyyy}";

    private static object SnapshotAudit(AttivitaConsulentePagamento p) => new
    {
        p.IdAttivitaConsulente,
        p.DataPagamento,
        p.Importo,
        p.Nota,
    };
}
