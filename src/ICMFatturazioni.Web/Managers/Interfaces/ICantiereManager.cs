using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Managers.Interfaces;

/// <summary>
/// Business logic per i cantieri (vista <c>fatt.Cantiere</c> su
/// <c>dbo.Cantiere</c> — entità condivisa con ICMVerbali).
/// </summary>
public interface ICantiereManager
{
    /// <summary>Cantieri attivi, ordinati per Ubicazione.</summary>
    Task<IReadOnlyList<Cantiere>> ElencoAsync(CancellationToken cancellationToken = default);

    /// <summary>Cantieri attivi di una specifica attività, ordinati per Ubicazione.</summary>
    Task<IReadOnlyList<Cantiere>> ElencoPerAttivitaAsync(Guid idAttivita, CancellationToken cancellationToken = default);

    /// <summary>Restituisce un cantiere per id, o null.</summary>
    Task<Cantiere?> GetByIdAsync(Guid idCantiere, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea un nuovo cantiere. Valida campi obbligatori, lunghezze e
    /// l'esistenza dell'attività associata. Restituisce l'id assegnato.
    /// </summary>
    Task<Guid> CreaAsync(Cantiere cantiere, CancellationToken cancellationToken = default);

    /// <summary>Aggiorna un cantiere esistente (stesse validazioni della creazione).</summary>
    Task AggiornaAsync(Cantiere cantiere, CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina (soft-delete) un cantiere. I verbali che lo referenziano non si
    /// rompono: la riga resta a DB con <c>IsAttivo = 0</c>.
    /// </summary>
    Task EliminaAsync(Guid idCantiere, CancellationToken cancellationToken = default);
}
