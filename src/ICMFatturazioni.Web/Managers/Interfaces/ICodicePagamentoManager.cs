using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Web.Managers.Interfaces;

/// <summary>
/// Logica applicativa sul catalogo Codici di pagamento. Tutte le operazioni di
/// UI passano da qui.
/// </summary>
public interface ICodicePagamentoManager
{
    /// <summary>Elenco dei codici attivi (con tipo/descrizioni risolte) per la griglia.</summary>
    Task<IReadOnlyList<CodicePagamentoRiga>> ElencoAsync(CancellationToken cancellationToken = default);

    /// <summary>Recupera il codice (entità grezza) per id, per il form di modifica.</summary>
    Task<CodicePagamento?> GetByIdAsync(Guid idCodicePagamento, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea un nuovo codice. Genera la PK GUID, valida (descrizione e tipo
    /// obbligatori; NumScadenze 1..3; coerenza giorni/scadenze; giorni aggiuntivi
    /// solo con fine mese; descrizione univoca) e rilancia
    /// <see cref="CodicePagamentoInvalidaException"/>. Ritorna l'id assegnato.
    /// </summary>
    Task<Guid> CreaAsync(CodicePagamento codice, CancellationToken cancellationToken = default);

    /// <summary>Aggiorna un codice esistente. Stesse validazioni di <see cref="CreaAsync"/>.</summary>
    Task AggiornaAsync(CodicePagamento codice, CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina (soft-delete) un codice. Solleva
    /// <see cref="CodicePagamentoConDipendenzeException"/> se usato da anagrafiche attive.
    /// </summary>
    Task EliminaAsync(Guid idCodicePagamento, CancellationToken cancellationToken = default);

    /// <summary>Verifica se il codice è eliminabile (nessuna dipendenza).</summary>
    Task<bool> EEliminabileAsync(Guid idCodicePagamento, CancellationToken cancellationToken = default);
}
