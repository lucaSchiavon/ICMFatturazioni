using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Managers.Interfaces;

/// <summary>Business logic per le attività clienti (fatt.Attivita).</summary>
public interface IAttivitaManager
{
    /// <summary>Tutte le attività attive, ordinate per Numero desc.</summary>
    Task<IReadOnlyList<Attivita>> ElencoAsync(CancellationToken cancellationToken = default);

    /// <summary>Attività attive di una specifica anagrafica, ordinate per Numero desc.</summary>
    Task<IReadOnlyList<Attivita>> ElencoPerAnagraficaAsync(Guid idAnagrafica, CancellationToken cancellationToken = default);

    /// <summary>Attività attive per una specifica combo anagrafica+tipo, ordinate per Numero desc. Usato dalla maschera a tre selettori.</summary>
    Task<IReadOnlyList<Attivita>> ElencoPerAnagraficaTipoAsync(Guid idAnagrafica, Guid idTipoAttivita, CancellationToken cancellationToken = default);

    /// <summary>Restituisce un'attività per id, o null.</summary>
    Task<Attivita?> GetByIdAsync(Guid idAttivita, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea una nuova attività. Valida campi obbligatori e coerenza date.
    /// Restituisce l'id assegnato.
    /// </summary>
    Task<Guid> CreaAsync(Attivita attivita, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggiorna un'attività esistente. Valida campi obbligatori e coerenza date.
    /// </summary>
    Task AggiornaAsync(Attivita attivita, CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina (soft-delete) un'attività, se non ha dettagli attivi.
    /// Lancia <see cref="AttivitaConDipendenzeException"/> in caso contrario.
    /// </summary>
    Task EliminaAsync(Guid idAttivita, CancellationToken cancellationToken = default);

    /// <summary>Restituisce true se l'attività può essere eliminata (nessun dettaglio attivo).</summary>
    Task<bool> EEliminabileAsync(Guid idAttivita, CancellationToken cancellationToken = default);
}
