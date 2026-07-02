using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Managers.Interfaces;

/// <summary>
/// Logica applicativa sui dati dello studio emittente (<c>fatt.Azienda</c>).
/// Sola lettura per ora: la gestione da UI sarà un vertical dedicato futuro.
/// </summary>
public interface IAziendaManager
{
    /// <summary>
    /// Restituisce l'azienda corrente (dati del cedente per l'intestazione dei
    /// documenti), o <c>null</c> se non configurata.
    /// </summary>
    Task<Azienda?> GetAziendaAsync(CancellationToken ct = default);
}
