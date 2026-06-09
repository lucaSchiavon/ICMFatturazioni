using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Repositories.Interfaces;

/// <summary>
/// Accesso dati alla tabella <c>fatt.Anagrafica</c>. Consumato dal solo
/// <see cref="Managers.Interfaces.IAnagraficaManager"/>.
/// </summary>
public interface IAnagraficaRepository
{
    /// <summary>
    /// Recupera le anagrafiche <b>attive</b> (<c>IsAttivo = 1</c>) ordinate per
    /// <see cref="Anagrafica.RagioneSociale"/>. Adatta a popolare il
    /// <c>MudDataGrid</c> dell'elenco: per ora niente paginazione, l'elenco
    /// demo ha qualche centinaio di righe e MudBlazor virtualizza lato client.
    /// Le disattivate (soft-delete) non compaiono in elenco.
    /// </summary>
    Task<IReadOnlyList<Anagrafica>> GetAttiviAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Recupera l'anagrafica per id (anche se disattivata), o <c>null</c> se
    /// inesistente.
    /// </summary>
    Task<Anagrafica?> GetByIdAsync(Guid idAnagrafica, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserisce una nuova anagrafica. L'<c>IdAnagrafica</c> (GUID UUIDv7) è
    /// già valorizzato dal manager prima della chiamata (generazione app-side,
    /// ADR D22): qui si esegue solo l'INSERT.
    /// </summary>
    Task InsertAsync(Anagrafica anagrafica, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggiorna un'anagrafica esistente. Tutti i campi sono sovrascritti
    /// con il valore presente in <paramref name="anagrafica"/>: chi chiama
    /// deve passare l'oggetto completo, non un patch.
    /// </summary>
    Task UpdateAsync(Anagrafica anagrafica, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-delete: disattiva l'anagrafica (<c>IsAttivo = 0</c>), non la
    /// rimuove fisicamente (ADR D22, uniformità con ICMVerbali). La verifica
    /// delle dipendenze è del manager.
    /// </summary>
    Task DisattivaAsync(Guid idAnagrafica, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se l'anagrafica è referenziata da entità a valle (progetti,
    /// avvisi, fatture). In Fase 2 non esistono ancora entità dipendenti,
    /// quindi ritorna sempre <c>false</c>; il metodo c'è per riservare il
    /// punto d'estensione e per il pattern visibility-driven della UI
    /// ("pulsante Elimina nascosto se ci sono dipendenze").
    /// </summary>
    Task<bool> HasDipendenzeAsync(Guid idAnagrafica, CancellationToken cancellationToken = default);
}
