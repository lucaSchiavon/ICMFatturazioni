using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Repositories.Interfaces;

/// <summary>
/// Accesso dati per i cantieri (vista aggiornabile <c>fatt.Cantiere</c> su
/// <c>dbo.Cantiere</c> del DB unificato — entità condivisa con ICMVerbali).
/// </summary>
public interface ICantiereRepository
{
    /// <summary>Cantieri attivi (soft-delete escluso), ordinati per Ubicazione.</summary>
    Task<IReadOnlyList<Cantiere>> GetAttiviAsync(CancellationToken cancellationToken = default);

    /// <summary>Cantieri attivi di una specifica attività, ordinati per Ubicazione.</summary>
    Task<IReadOnlyList<Cantiere>> GetByAttivitaAsync(Guid idAttivita, CancellationToken cancellationToken = default);

    /// <summary>Cantiere per id, o <c>null</c> se inesistente.</summary>
    Task<Cantiere?> GetByIdAsync(Guid idCantiere, CancellationToken cancellationToken = default);

    /// <summary>Inserisce un cantiere. L'IdCantiere arriva già valorizzato dal manager (GUID v7 app-side).</summary>
    Task InsertAsync(Cantiere cantiere, CancellationToken cancellationToken = default);

    /// <summary>Aggiorna un cantiere esistente.</summary>
    Task UpdateAsync(Cantiere cantiere, CancellationToken cancellationToken = default);

    /// <summary>Soft-delete (ADR D22): disattiva senza rimuovere fisicamente.</summary>
    Task DisattivaAsync(Guid idCantiere, CancellationToken cancellationToken = default);
}
