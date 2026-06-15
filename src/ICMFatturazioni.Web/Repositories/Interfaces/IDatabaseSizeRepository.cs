namespace ICMFatturazioni.Web.Repositories.Interfaces;

/// <summary>
/// Lettura della dimensione corrente del database, per la sentinella sui 10 GB
/// di SQL Server Express (vedi docs/audit-dimensionamento-sql-express.pdf §6).
/// </summary>
public interface IDatabaseSizeRepository
{
    /// <summary>
    /// Dimensione totale dei <b>file dati</b> del database (esclusi i log di
    /// transazione), in MB. È la metrica che conta verso il tetto dei 10 GB di
    /// Express.
    /// </summary>
    Task<int> GetDimensioneDatiMbAsync(CancellationToken cancellationToken = default);
}
