using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Managers.Interfaces;

/// <summary>
/// Logica applicativa sul modulo Anagrafica. Tutte le operazioni di UI
/// passano da qui: la UI non accede mai a <c>IAnagraficaRepository</c>.
/// </summary>
public interface IAnagraficaManager
{
    /// <summary>
    /// Elenco completo delle anagrafiche, ordinate per ragione sociale.
    /// Pronto per essere consumato dal <c>MudDataGrid</c>.
    /// </summary>
    Task<IReadOnlyList<Anagrafica>> ElencoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Recupera un'anagrafica per id, o <c>null</c> se non esiste.
    /// </summary>
    Task<Anagrafica?> GetByIdAsync(int idAnagrafica, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea una nuova anagrafica. Esegue le validazioni di forma e
    /// rilancia <see cref="AnagraficaInvalidaException"/> con motivo
    /// specifico in caso di errore. Ritorna l'<c>IdAnagrafica</c> assegnato.
    /// </summary>
    Task<int> CreaAsync(Anagrafica anagrafica, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggiorna un'anagrafica esistente. Stesse validazioni di
    /// <see cref="CreaAsync"/>; l'<c>IdAnagrafica</c> in
    /// <paramref name="anagrafica"/> identifica la riga.
    /// </summary>
    Task AggiornaAsync(Anagrafica anagrafica, CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina un'anagrafica. Solleva
    /// <see cref="AnagraficaConDipendenzeException"/> se l'anagrafica è
    /// referenziata da entità a valle.
    /// </summary>
    Task EliminaAsync(int idAnagrafica, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se l'anagrafica è eliminabile (cioè se NON ha dipendenze).
    /// Usata dalla UI per decidere se mostrare il pulsante "Elimina"
    /// (pattern visibility-driven da CLAUDE.md).
    /// </summary>
    Task<bool> EEliminabileAsync(int idAnagrafica, CancellationToken cancellationToken = default);
}
