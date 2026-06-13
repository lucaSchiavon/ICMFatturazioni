using ICMFatturazioni.Web.Auditing;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Repositories.Interfaces;
using Microsoft.Data.SqlClient;

namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Implementazione di <see cref="ITipoPagamentoManager"/>:
///   1) validazione (descrizione obbligatoria; descrizione e sigla univoche),
///   2) normalizzazione (trim; sigla in maiuscolo; vuoto → null),
///   3) traduzione SqlException (indici UNIQUE) in
///      <see cref="TipoPagamentoInvalidaException"/>,
///   4) doppia difesa su DELETE,
///   5) audit di ogni scrittura (Regola 7).
/// </summary>
internal sealed class TipoPagamentoManager : ITipoPagamentoManager
{
    private const int SqlErrorDuplicateIndexKey = 2601;
    private const int SqlErrorDuplicateConstraint = 2627;

    private const string EntityType = nameof(TipoPagamento);

    private readonly ITipoPagamentoRepository _repository;
    private readonly IAuditManager _audit;

    public TipoPagamentoManager(ITipoPagamentoRepository repository, IAuditManager audit)
    {
        _repository = repository;
        _audit = audit;
    }

    public Task<IReadOnlyList<TipoPagamento>> ElencoAsync(CancellationToken cancellationToken = default)
        => _repository.GetAttiviAsync(cancellationToken);

    public Task<TipoPagamento?> GetByIdAsync(Guid idTipoPagamento, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(idTipoPagamento, cancellationToken);

    public async Task<Guid> CreaAsync(TipoPagamento tipo, CancellationToken cancellationToken = default)
    {
        var norm = Normalizza(tipo);
        ValidaCampi(norm);
        await ValidaUnicitaAsync(norm, escludiId: null, cancellationToken);

        norm.IdTipoPagamento = Guid.CreateVersion7();
        try
        {
            await _repository.InsertAsync(norm, cancellationToken);
            await _audit.RegistraCreazioneAsync(EntityType, norm.IdTipoPagamento, norm.Descrizione,
                AuditDettaglio.Snapshot(norm), cancellationToken);
            return norm.IdTipoPagamento;
        }
        catch (SqlException ex) when (IsViolazioneVincolo(ex))
        {
            throw TraduciViolazione(ex);
        }
    }

    public async Task AggiornaAsync(TipoPagamento tipo, CancellationToken cancellationToken = default)
    {
        var norm = Normalizza(tipo);
        ValidaCampi(norm);
        await ValidaUnicitaAsync(norm, escludiId: norm.IdTipoPagamento, cancellationToken);

        var precedente = await _repository.GetByIdAsync(norm.IdTipoPagamento, cancellationToken);
        try
        {
            await _repository.UpdateAsync(norm, cancellationToken);
            var dati = precedente is null
                ? AuditDettaglio.Snapshot(norm)
                : AuditDettaglio.Diff(precedente, norm);
            await _audit.RegistraModificaAsync(EntityType, norm.IdTipoPagamento, norm.Descrizione, dati, cancellationToken);
        }
        catch (SqlException ex) when (IsViolazioneVincolo(ex))
        {
            throw TraduciViolazione(ex);
        }
    }

    public async Task EliminaAsync(Guid idTipoPagamento, CancellationToken cancellationToken = default)
    {
        if (await _repository.HasDipendenzeAsync(idTipoPagamento, cancellationToken))
        {
            throw new TipoPagamentoConDipendenzeException(idTipoPagamento);
        }

        var tipo = await _repository.GetByIdAsync(idTipoPagamento, cancellationToken);
        await _repository.DisattivaAsync(idTipoPagamento, cancellationToken);
        var dati = tipo is null ? null : AuditDettaglio.Snapshot(tipo);
        await _audit.RegistraEliminazioneAsync(EntityType, idTipoPagamento, tipo?.Descrizione, dati, cancellationToken);
    }

    public async Task<bool> EEliminabileAsync(Guid idTipoPagamento, CancellationToken cancellationToken = default)
        => !await _repository.HasDipendenzeAsync(idTipoPagamento, cancellationToken);

    // ---------------------------------------------------------------------
    // Helper privati
    // ---------------------------------------------------------------------

    private static TipoPagamento Normalizza(TipoPagamento t) => new()
    {
        IdTipoPagamento = t.IdTipoPagamento,
        Descrizione     = t.Descrizione?.Trim() ?? string.Empty,
        // Sigla in maiuscolo per uniformità (BO, RB); vuoto → null.
        SiglaPag        = string.IsNullOrWhiteSpace(t.SiglaPag) ? null : t.SiglaPag.Trim().ToUpperInvariant(),
        FlagBanca       = t.FlagBanca,
        IsAttivo        = t.IsAttivo,
    };

    private static void ValidaCampi(TipoPagamento t)
    {
        if (string.IsNullOrWhiteSpace(t.Descrizione))
        {
            throw new TipoPagamentoInvalidaException(
                TipoPagamentoInvalidoMotivo.DescrizioneObbligatoria,
                "La descrizione del tipo di pagamento è obbligatoria.");
        }
    }

    private async Task ValidaUnicitaAsync(TipoPagamento t, Guid? escludiId, CancellationToken cancellationToken)
    {
        if (await _repository.ExistsDescrizioneAttivaAsync(t.Descrizione, escludiId, cancellationToken))
        {
            throw new TipoPagamentoInvalidaException(
                TipoPagamentoInvalidoMotivo.DescrizioneDuplicata,
                $"Esiste già un tipo di pagamento attivo con descrizione \"{t.Descrizione}\".");
        }
        if (await _repository.ExistsSiglaAttivaAsync(t.SiglaPag, escludiId, cancellationToken))
        {
            throw new TipoPagamentoInvalidaException(
                TipoPagamentoInvalidoMotivo.SiglaDuplicata,
                $"Esiste già un tipo di pagamento attivo con sigla \"{t.SiglaPag}\".");
        }
    }

    private static bool IsViolazioneVincolo(SqlException ex)
        => ex.Number is SqlErrorDuplicateIndexKey or SqlErrorDuplicateConstraint;

    private static TipoPagamentoInvalidaException TraduciViolazione(SqlException ex)
    {
        var msg = ex.Message;
        if (msg.Contains("UX_TipiPagamento_Sigla", StringComparison.OrdinalIgnoreCase))
        {
            return new TipoPagamentoInvalidaException(
                TipoPagamentoInvalidoMotivo.SiglaDuplicata,
                "Esiste già un tipo di pagamento attivo con questa sigla.", ex);
        }
        // UX_TipiPagamento_Descr o vincolo inatteso.
        return new TipoPagamentoInvalidaException(
            TipoPagamentoInvalidoMotivo.DescrizioneDuplicata,
            "Esiste già un tipo di pagamento attivo con questa descrizione.", ex);
    }
}
