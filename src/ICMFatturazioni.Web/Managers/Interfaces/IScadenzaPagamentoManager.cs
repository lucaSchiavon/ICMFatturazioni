using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Managers.Interfaces;

/// <summary>
/// Orchestrazione business per le scadenze di pagamento di un dettaglio (cap. 10.4).
/// Regole chiave:
///   • Non è possibile modificare/eliminare scadenze di un dettaglio con <c>HasFattura = true</c>.
///   • La somma delle scadenze vs l'importo del dettaglio è solo un avviso nella UI (non bloccante).
/// </summary>
public interface IScadenzaPagamentoManager
{
    /// <summary>Restituisce le scadenze attive di un dettaglio, ordinate per DataScadenza ASC.</summary>
    Task<IReadOnlyList<ScadenzaPagamento>> ElencoPerDettaglioAsync(Guid idAttivitaDettaglio, CancellationToken ct = default);

    /// <summary>
    /// Crea una nuova scadenza. Assegna GUID v7.
    /// Lancia <see cref="ScadenzaPagamentoInvalidaException"/> se la validazione fallisce.
    /// </summary>
    Task<Guid> CreaAsync(ScadenzaPagamento scadenza, CancellationToken ct = default);

    /// <summary>
    /// Aggiorna una scadenza esistente.
    /// Lancia <see cref="ScadenzaPagamentoInvalidaException"/> se la validazione fallisce
    /// o se il dettaglio parent ha <c>HasFattura = true</c>.
    /// </summary>
    Task AggiornaAsync(ScadenzaPagamento scadenza, CancellationToken ct = default);

    /// <summary>
    /// Soft-delete della scadenza.
    /// Lancia <see cref="ScadenzaPagamentoInvalidaException"/> se il dettaglio parent ha <c>HasFattura = true</c>.
    /// </summary>
    Task EliminaAsync(Guid idScadenza, CancellationToken ct = default);
}
