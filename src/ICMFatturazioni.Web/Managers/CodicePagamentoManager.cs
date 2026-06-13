using ICMFatturazioni.Web.Auditing;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Models;
using ICMFatturazioni.Web.Repositories.Interfaces;
using Microsoft.Data.SqlClient;

namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Implementazione di <see cref="ICodicePagamentoManager"/>:
///   1) normalizzazione (trim; azzeramento dei GGScad non pertinenti al numero
///      di rate; GGpiu a null se ≤ 0; codici lookup trim→null),
///   2) validazioni (cap. 4): descrizione e tipo obbligatori; NumScadenze 1..3;
///      coerenza giorni↔scadenze; giorni aggiuntivi solo con fine mese;
///      descrizione univoca,
///   3) audit (Regola 7),
///   4) doppia difesa su DELETE + traduzione SqlException.
/// </summary>
internal sealed class CodicePagamentoManager : ICodicePagamentoManager
{
    private const int SqlErrorConstraintViolation = 547;
    private const int SqlErrorDuplicateIndexKey = 2601;
    private const int SqlErrorDuplicateConstraint = 2627;

    private const string EntityType = nameof(CodicePagamento);

    private readonly ICodicePagamentoRepository _repository;
    private readonly IAuditManager _audit;

    public CodicePagamentoManager(ICodicePagamentoRepository repository, IAuditManager audit)
    {
        _repository = repository;
        _audit = audit;
    }

    public Task<IReadOnlyList<CodicePagamentoRiga>> ElencoAsync(CancellationToken cancellationToken = default)
        => _repository.GetAttiviAsync(cancellationToken);

    public Task<CodicePagamento?> GetByIdAsync(Guid idCodicePagamento, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(idCodicePagamento, cancellationToken);

    public async Task<Guid> CreaAsync(CodicePagamento codice, CancellationToken cancellationToken = default)
    {
        var norm = Normalizza(codice);
        ValidaCampi(norm);
        await ValidaUnicitaAsync(norm, escludiId: null, cancellationToken);

        norm.IdCodicePagamento = Guid.CreateVersion7();
        try
        {
            await _repository.InsertAsync(norm, cancellationToken);
            await _audit.RegistraCreazioneAsync(EntityType, norm.IdCodicePagamento, norm.DescrPag,
                AuditDettaglio.Snapshot(norm), cancellationToken);
            return norm.IdCodicePagamento;
        }
        catch (SqlException ex) when (IsViolazioneVincolo(ex))
        {
            throw TraduciViolazione(ex);
        }
    }

    public async Task AggiornaAsync(CodicePagamento codice, CancellationToken cancellationToken = default)
    {
        var norm = Normalizza(codice);
        ValidaCampi(norm);
        await ValidaUnicitaAsync(norm, escludiId: norm.IdCodicePagamento, cancellationToken);

        var precedente = await _repository.GetByIdAsync(norm.IdCodicePagamento, cancellationToken);
        try
        {
            await _repository.UpdateAsync(norm, cancellationToken);
            var dati = precedente is null
                ? AuditDettaglio.Snapshot(norm)
                : AuditDettaglio.Diff(precedente, norm);
            await _audit.RegistraModificaAsync(EntityType, norm.IdCodicePagamento, norm.DescrPag, dati, cancellationToken);
        }
        catch (SqlException ex) when (IsViolazioneVincolo(ex))
        {
            throw TraduciViolazione(ex);
        }
    }

    public async Task EliminaAsync(Guid idCodicePagamento, CancellationToken cancellationToken = default)
    {
        if (await _repository.HasDipendenzeAsync(idCodicePagamento, cancellationToken))
        {
            throw new CodicePagamentoConDipendenzeException(idCodicePagamento);
        }

        var codice = await _repository.GetByIdAsync(idCodicePagamento, cancellationToken);
        await _repository.DisattivaAsync(idCodicePagamento, cancellationToken);
        var dati = codice is null ? null : AuditDettaglio.Snapshot(codice);
        await _audit.RegistraEliminazioneAsync(EntityType, idCodicePagamento, codice?.DescrPag, dati, cancellationToken);
    }

    public async Task<bool> EEliminabileAsync(Guid idCodicePagamento, CancellationToken cancellationToken = default)
        => !await _repository.HasDipendenzeAsync(idCodicePagamento, cancellationToken);

    // ---------------------------------------------------------------------
    // Helper privati
    // ---------------------------------------------------------------------

    /// <summary>
    /// Normalizza: trim descrizione/codici; azzera i GGScad non pertinenti al
    /// numero di rate; GGpiu a null se ≤ 0; GGpiu a null se non fine mese (la
    /// validazione esplicita scatta comunque su un GGpiu positivo senza f.m.).
    /// </summary>
    private static CodicePagamento Normalizza(CodicePagamento c)
    {
        var ggPiu = c.GGpiu is > 0 ? c.GGpiu : null;
        return new CodicePagamento
        {
            IdCodicePagamento = c.IdCodicePagamento,
            IdTipoPagamento = c.IdTipoPagamento,
            DescrPag = c.DescrPag?.Trim() ?? string.Empty,
            NumScadenze = c.NumScadenze,
            GGScad1 = c.GGScad1,
            GGScad2 = c.NumScadenze >= 2 ? c.GGScad2 : null,
            GGScad3 = c.NumScadenze >= 3 ? c.GGScad3 : null,
            GGpiu = ggPiu,
            FineMese = c.FineMese,
            CondizionePagamento = Pulisci(c.CondizionePagamento),
            ModalitaPagamento = Pulisci(c.ModalitaPagamento),
            IsAttivo = c.IsAttivo,
        };
    }

    private static string? Pulisci(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static void ValidaCampi(CodicePagamento c)
    {
        if (string.IsNullOrWhiteSpace(c.DescrPag))
        {
            throw new CodicePagamentoInvalidaException(
                CodicePagamentoInvalidoMotivo.DescrizioneObbligatoria,
                "La descrizione del codice di pagamento è obbligatoria.");
        }
        if (c.IdTipoPagamento == Guid.Empty)
        {
            throw new CodicePagamentoInvalidaException(
                CodicePagamentoInvalidoMotivo.TipoObbligatorio,
                "Selezionare il tipo di pagamento.");
        }
        if (c.NumScadenze is < 1 or > 3)
        {
            throw new CodicePagamentoInvalidaException(
                CodicePagamentoInvalidoMotivo.NumScadenzeNonValido,
                "Il numero di scadenze deve essere tra 1 e 3.");
        }
        // Coerenza giorni ↔ numero di scadenze: servono i GG fino a NumScadenze.
        if (c.NumScadenze >= 2 && c.GGScad2 is null)
        {
            throw new CodicePagamentoInvalidaException(
                CodicePagamentoInvalidoMotivo.GiorniScadenzaIncoerenti,
                "Con 2 o più scadenze va indicato il valore di giorni della 2ª scadenza.");
        }
        if (c.NumScadenze >= 3 && c.GGScad3 is null)
        {
            throw new CodicePagamentoInvalidaException(
                CodicePagamentoInvalidoMotivo.GiorniScadenzaIncoerenti,
                "Con 3 scadenze va indicato il valore di giorni della 3ª scadenza.");
        }
        // Giorni aggiuntivi solo con fine mese (cap. 4).
        if (c.GGpiu is > 0 && !c.FineMese)
        {
            throw new CodicePagamentoInvalidaException(
                CodicePagamentoInvalidoMotivo.GiorniAggiuntiviSenzaFineMese,
                "I giorni aggiuntivi sono ammessi solo con lo spostamento a fine mese.");
        }
    }

    private async Task ValidaUnicitaAsync(CodicePagamento c, Guid? escludiId, CancellationToken cancellationToken)
    {
        if (await _repository.ExistsDescrizioneAttivaAsync(c.DescrPag, escludiId, cancellationToken))
        {
            throw new CodicePagamentoInvalidaException(
                CodicePagamentoInvalidoMotivo.DescrizioneDuplicata,
                $"Esiste già un codice di pagamento attivo con descrizione \"{c.DescrPag}\".");
        }
    }

    private static bool IsViolazioneVincolo(SqlException ex)
        => ex.Number is SqlErrorConstraintViolation or SqlErrorDuplicateIndexKey or SqlErrorDuplicateConstraint;

    private static CodicePagamentoInvalidaException TraduciViolazione(SqlException ex)
    {
        var msg = ex.Message;
        if (msg.Contains("FK_CodiciPagamento_Tipo", StringComparison.OrdinalIgnoreCase))
        {
            return new CodicePagamentoInvalidaException(
                CodicePagamentoInvalidoMotivo.TipoInesistente,
                "Il tipo di pagamento indicato non è valido. Sceglierlo dall'elenco.", ex);
        }
        if (msg.Contains("UX_CodiciPagamento_Descr", StringComparison.OrdinalIgnoreCase))
        {
            return new CodicePagamentoInvalidaException(
                CodicePagamentoInvalidoMotivo.DescrizioneDuplicata,
                "Esiste già un codice di pagamento attivo con questa descrizione.", ex);
        }
        if (msg.Contains("CK_CodiciPagamento_GGpiu", StringComparison.OrdinalIgnoreCase))
        {
            return new CodicePagamentoInvalidaException(
                CodicePagamentoInvalidoMotivo.GiorniAggiuntiviSenzaFineMese,
                "I giorni aggiuntivi sono ammessi solo con lo spostamento a fine mese.", ex);
        }
        return new CodicePagamentoInvalidaException(
            CodicePagamentoInvalidoMotivo.NumScadenzeNonValido,
            "I dati del codice di pagamento non rispettano un vincolo.", ex);
    }
}
