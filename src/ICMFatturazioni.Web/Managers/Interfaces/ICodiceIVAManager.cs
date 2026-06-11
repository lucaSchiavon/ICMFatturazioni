using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Managers.Interfaces;

/// <summary>
/// Logica applicativa sul catalogo Codici IVA. Tutte le operazioni di UI
/// passano da qui: la UI non accede mai a <c>ICodiceIVARepository</c>.
/// </summary>
public interface ICodiceIVAManager
{
    /// <summary>
    /// Elenco dei codici IVA attivi, ordinati per codice. Pronto per il
    /// <c>MudDataGrid</c>.
    /// </summary>
    Task<IReadOnlyList<CodiceIVA>> ElencoAsync(CancellationToken cancellationToken = default);

    /// <summary>Recupera un codice IVA per id, o <c>null</c> se non esiste.</summary>
    Task<CodiceIVA?> GetByIdAsync(Guid idCodiceIVA, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea un nuovo codice IVA. Genera la PK GUID (UUIDv7 app-side), esegue le
    /// validazioni (Codice/Descrizione obbligatori e unici, Aliquota ≥ 0,
    /// regola Natura ⟺ Aliquota = 0) e rilancia
    /// <see cref="CodiceIVAInvalidaException"/> con motivo specifico. Ritorna
    /// l'<c>IdCodiceIVA</c> assegnato.
    /// </summary>
    Task<Guid> CreaAsync(CodiceIVA codiceIva, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggiorna un codice IVA esistente. Stesse validazioni di
    /// <see cref="CreaAsync"/>; l'<c>IdCodiceIVA</c> identifica la riga.
    /// </summary>
    Task AggiornaAsync(CodiceIVA codiceIva, CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina (soft-delete: disattiva) un codice IVA. Solleva
    /// <see cref="CodiceIVAConDipendenzeException"/> se è ancora usato da
    /// anagrafiche attive.
    /// </summary>
    Task EliminaAsync(Guid idCodiceIVA, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se il codice IVA è eliminabile (cioè se NON ha dipendenze).
    /// Usata dalla UI per il pattern visibility-driven del pulsante "Elimina".
    /// </summary>
    Task<bool> EEliminabileAsync(Guid idCodiceIVA, CancellationToken cancellationToken = default);
}
