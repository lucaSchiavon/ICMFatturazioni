using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Repositories.Interfaces;

/// <summary>
/// Accesso dati alla tabella <c>ana.Anagrafica</c>. Consumato dal solo
/// <see cref="Managers.Interfaces.IAnagraficaManager"/>.
/// </summary>
public interface IAnagraficaRepository
{
    /// <summary>
    /// Recupera tutte le anagrafiche ordinate per <see cref="Anagrafica.RagioneSociale"/>.
    /// Adatta a popolare il <c>MudDataGrid</c> dell'elenco: per ora niente
    /// paginazione, l'elenco demo ha qualche centinaio di righe e MudBlazor
    /// virtualizza lato client.
    /// </summary>
    Task<IReadOnlyList<Anagrafica>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Recupera l'anagrafica per id, o <c>null</c> se inesistente.
    /// </summary>
    Task<Anagrafica?> GetByIdAsync(int idAnagrafica, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserisce una nuova anagrafica. Ritorna l'<c>IdAnagrafica</c>
    /// generato dall'identity.
    /// </summary>
    Task<int> InsertAsync(Anagrafica anagrafica, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggiorna un'anagrafica esistente. Tutti i campi sono sovrascritti
    /// con il valore presente in <paramref name="anagrafica"/>: chi chiama
    /// deve passare l'oggetto completo, non un patch.
    /// </summary>
    Task UpdateAsync(Anagrafica anagrafica, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancella l'anagrafica. Non verifica le dipendenze: la verifica è
    /// del manager, che ha la responsabilità di sollevare l'eccezione
    /// tipizzata in caso di violazione (pattern "doppia difesa": pre-check
    /// nel manager, vincolo FK nel DB).
    /// </summary>
    Task DeleteAsync(int idAnagrafica, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se l'anagrafica è referenziata da entità a valle (progetti,
    /// avvisi, fatture). In Fase 2 non esistono ancora entità dipendenti,
    /// quindi ritorna sempre <c>false</c>; il metodo c'è per riservare il
    /// punto d'estensione e per il pattern visibility-driven della UI
    /// ("pulsante Elimina nascosto se ci sono dipendenze").
    /// </summary>
    Task<bool> HasDipendenzeAsync(int idAnagrafica, CancellationToken cancellationToken = default);
}
