using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Managers.Interfaces;

/// <summary>
/// Logica applicativa sull'anagrafica istituti (<c>fatt.Banche</c>).
/// </summary>
public interface IBancaManager
{
    /// <summary>Istituti attivi, ordinati per nome. Alimenta la combo Banca.</summary>
    Task<IReadOnlyList<Banca>> ElencoAttiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Risolve un istituto per nome con logica "get-or-create": se esiste lo
    /// riusa (aggiornandone l'ABI quando l'utente ne fornisce uno diverso),
    /// altrimenti lo crea. Registra l'audit dei soli eventi reali (creazione o
    /// modifica ABI). Ritorna l'<c>IdBanca</c>.
    /// </summary>
    Task<Guid> RisolviAsync(string nome, string? abi, CancellationToken cancellationToken = default);
}
