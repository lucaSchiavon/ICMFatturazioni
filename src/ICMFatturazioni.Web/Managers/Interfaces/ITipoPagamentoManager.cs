using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Managers.Interfaces;

/// <summary>
/// Logica applicativa sul catalogo Tipi di pagamento. Tutte le operazioni di UI
/// passano da qui: la UI non accede mai a <c>ITipoPagamentoRepository</c>.
/// </summary>
public interface ITipoPagamentoManager
{
    /// <summary>Elenco dei tipi attivi, ordinati per descrizione.</summary>
    Task<IReadOnlyList<TipoPagamento>> ElencoAsync(CancellationToken cancellationToken = default);

    /// <summary>Recupera un tipo per id, o <c>null</c>.</summary>
    Task<TipoPagamento?> GetByIdAsync(Guid idTipoPagamento, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea un nuovo tipo. Genera la PK GUID, valida (descrizione obbligatoria e
    /// univoca, sigla univoca) e rilancia <see cref="TipoPagamentoInvalidaException"/>.
    /// Ritorna l'<c>IdTipoPagamento</c> assegnato.
    /// </summary>
    Task<Guid> CreaAsync(TipoPagamento tipo, CancellationToken cancellationToken = default);

    /// <summary>Aggiorna un tipo esistente. Stesse validazioni di <see cref="CreaAsync"/>.</summary>
    Task AggiornaAsync(TipoPagamento tipo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina (soft-delete) un tipo. Solleva
    /// <see cref="TipoPagamentoConDipendenzeException"/> se usato da codici di
    /// pagamento attivi.
    /// </summary>
    Task EliminaAsync(Guid idTipoPagamento, CancellationToken cancellationToken = default);

    /// <summary>Verifica se il tipo è eliminabile (nessuna dipendenza).</summary>
    Task<bool> EEliminabileAsync(Guid idTipoPagamento, CancellationToken cancellationToken = default);
}
