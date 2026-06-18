using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Repositories.Interfaces;

/// <summary>
/// Accesso ai dati per <c>fatt.AttivitaDettaglio</c>.
/// Ordinamento implicito: tutti i metodi di elenco restituiscono righe
/// ordinate per <c>Ordine ASC</c> (l'ordine 1-based del gestionale).
/// </summary>
public interface IAttivitaDettaglioRepository
{
    /// <summary>Restituisce i dettagli attivi di un'attività, ordinati per Ordine ASC.</summary>
    Task<IReadOnlyList<AttivitaDettaglio>> GetByAttivitaAsync(Guid idAttivita, CancellationToken ct = default);

    /// <summary>Restituisce un dettaglio per chiave primaria (inclusi soft-deleted).</summary>
    Task<AttivitaDettaglio?> GetByIdAsync(Guid idAttivitaDettaglio, CancellationToken ct = default);

    /// <summary>
    /// Restituisce il valore massimo di Ordine tra i dettagli attivi dell'attività.
    /// Restituisce 0 se l'attività non ha ancora dettagli.
    /// </summary>
    Task<int> GetMaxOrdineAsync(Guid idAttivita, CancellationToken ct = default);

    /// <summary>Inserisce un nuovo dettaglio.</summary>
    Task InsertAsync(AttivitaDettaglio dettaglio, CancellationToken ct = default);

    /// <summary>Aggiorna un dettaglio esistente (tutti i campi tranne IdAttivitaDettaglio e HasFattura).</summary>
    Task UpdateAsync(AttivitaDettaglio dettaglio, CancellationToken ct = default);

    /// <summary>Soft-delete: imposta IsAttivo = 0.</summary>
    Task DisattivaAsync(Guid idAttivitaDettaglio, CancellationToken ct = default);

    /// <summary>
    /// Scambia gli Ordine di due dettagli in una singola transazione.
    /// Usa un ordine temporaneo (-999) per rispettare il UNIQUE (IdAttivita, Ordine)
    /// durante l'operazione a tre passi.
    /// </summary>
    Task ScambiaOrdineAsync(Guid idA, Guid idB, int ordineA, int ordineB, CancellationToken ct = default);
}
