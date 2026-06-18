using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Repositories.Interfaces;

/// <summary>Accesso dati per <see cref="Attivita"/> (tabella fatt.Attivita).</summary>
public interface IAttivitaRepository
{
    /// <summary>Elenco attività attive di un'anagrafica, ordinate per Numero desc.</summary>
    Task<IReadOnlyList<Attivita>> GetByAnagraficaAsync(Guid idAnagrafica, CancellationToken cancellationToken = default);

    /// <summary>Elenco attività attive di una specifica combo anagrafica+tipo, ordinate per Numero desc.</summary>
    Task<IReadOnlyList<Attivita>> GetByAnagraficaETipoAsync(Guid idAnagrafica, Guid idTipoAttivita, CancellationToken cancellationToken = default);

    /// <summary>Tutte le attività attive (per amministrazione/ricerca globale), ordinate per Numero desc.</summary>
    Task<IReadOnlyList<Attivita>> GetAttiviAsync(CancellationToken cancellationToken = default);

    /// <summary>Restituisce l'attività per chiave primaria, o null se non trovata.</summary>
    Task<Attivita?> GetByIdAsync(Guid idAttivita, CancellationToken cancellationToken = default);

    /// <summary>Inserisce una nuova attività. Il campo <see cref="Attivita.Numero"/> viene popolato dall'IDENTITY del DB.</summary>
    Task InsertAsync(Attivita attivita, CancellationToken cancellationToken = default);

    /// <summary>Aggiorna i campi modificabili di un'attività esistente.</summary>
    Task UpdateAsync(Attivita attivita, CancellationToken cancellationToken = default);

    /// <summary>Soft-delete: imposta IsAttivo = 0.</summary>
    Task DisattivaAsync(Guid idAttivita, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se l'attività ha dettagli attivi collegati (fatt.AttivitaDettaglio).
    /// Dipende da migration 027; restituisce false se la tabella non esiste ancora.
    /// </summary>
    Task<bool> HasDipendenzeAsync(Guid idAttivita, CancellationToken cancellationToken = default);
}
