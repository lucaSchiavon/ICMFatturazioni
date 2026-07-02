using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Repositories.Interfaces;

/// <summary>
/// Accesso ai dati per <c>fatt.Azienda</c> (dati dello studio emittente).
/// Sistema mono-studio: la lettura principale restituisce "l'azienda corrente".
/// </summary>
public interface IAziendaRepository
{
    /// <summary>
    /// Restituisce l'azienda corrente (la prima riga attiva), o <c>null</c> se
    /// non configurata.
    /// </summary>
    Task<Azienda?> GetAziendaAsync(CancellationToken ct = default);
}
