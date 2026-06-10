using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Managers.Interfaces;

/// <summary>
/// Logica applicativa sui ruoli. In T1 sono sole letture (servono ad
/// autenticazione e seed); creazione/modifica dei ruoli custom arriveranno
/// con la UI di amministrazione (T3).
/// </summary>
public interface IRuoloManager
{
    Task<IReadOnlyList<Ruolo>> ElencoAsync(CancellationToken cancellationToken = default);
    Task<Ruolo?> GetByIdAsync(Guid idRuolo, CancellationToken cancellationToken = default);
    Task<Ruolo?> GetByCodiceAsync(string codice, CancellationToken cancellationToken = default);
}
