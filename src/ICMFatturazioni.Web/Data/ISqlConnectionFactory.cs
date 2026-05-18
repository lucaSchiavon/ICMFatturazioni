using System.Data;

namespace ICMFatturazioni.Web.Data;

/// <summary>
/// Astrazione per la creazione di connessioni SQL Server.
/// I Repository dipendono da questa interfaccia (DI) e non da
/// <c>SqlConnection</c> direttamente: questo permette di sostituire
/// la sorgente della connection string in test e di mantenere il
/// codice di accesso dati disaccoppiato dal driver concreto.
/// </summary>
public interface ISqlConnectionFactory
{
    /// <summary>
    /// Restituisce una nuova connessione <b>già aperta</b> verso il
    /// database configurato. Il chiamante è responsabile del dispose
    /// (di norma con <c>await using</c>).
    /// </summary>
    Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
}
