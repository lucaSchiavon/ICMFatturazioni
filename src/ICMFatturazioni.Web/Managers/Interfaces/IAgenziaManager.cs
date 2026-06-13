using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Managers.Interfaces;

/// <summary>
/// Logica applicativa sull'anagrafica filiali (<c>fatt.Agenzie</c>).
/// </summary>
public interface IAgenziaManager
{
    /// <summary>
    /// Filiali attive di una banca, ordinate per nome. Alimenta la combo Agenzia
    /// (filtrata per la banca scelta).
    /// </summary>
    Task<IReadOnlyList<Agenzia>> GetByBancaAsync(Guid idBanca, CancellationToken cancellationToken = default);

    /// <summary>
    /// Risolve una filiale (di una banca) per nome con logica "get-or-create":
    /// se esiste la riusa (aggiornandone il CAB quando l'utente ne fornisce uno
    /// diverso), altrimenti la crea. Se <paramref name="nome"/> è vuoto ritorna
    /// <c>null</c> (filiale non indicata). Registra l'audit dei soli eventi
    /// reali. Ritorna l'<c>IdAgenzia</c> o <c>null</c>.
    /// </summary>
    Task<Guid?> RisolviAsync(Guid idBanca, string? nome, string? cab, CancellationToken cancellationToken = default);
}
