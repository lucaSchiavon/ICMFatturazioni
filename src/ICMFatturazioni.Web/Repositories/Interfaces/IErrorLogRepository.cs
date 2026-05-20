using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Repositories.Interfaces;

/// <summary>
/// Repository dedicato per la scrittura su <c>dbo.LogErrors</c>.
/// Eccezione esplicita alla regola "1 repository ↔ 1 manager": il
/// logger è infrastrutturale (singleton iniettato in ogni layer) e non
/// ha un Manager di dominio corrispondente.
/// </summary>
public interface IErrorLogRepository
{
    /// <summary>
    /// Inserisce un evento di errore. Ritorna <c>true</c> se l'INSERT è
    /// riuscito, <c>false</c> se è stato impossibile scrivere (es. DB
    /// irraggiungibile, errore SqlClient). <b>Mai</b> lancia eccezioni:
    /// è un canale "infallibile" dal punto di vista del chiamante.
    /// </summary>
    Task<bool> InsertAsync(LogError entry, CancellationToken cancellationToken = default);
}
