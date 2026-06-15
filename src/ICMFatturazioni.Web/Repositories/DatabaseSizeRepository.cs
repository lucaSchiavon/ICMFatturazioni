using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Repositories;

/// <summary>
/// Implementazione Dapper di <see cref="IDatabaseSizeRepository"/> su
/// <c>sys.database_files</c>.
/// </summary>
internal sealed class DatabaseSizeRepository : IDatabaseSizeRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public DatabaseSizeRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    // size è espresso in pagine da 8 KB. type = 0 → file dati (ROWS); 1 → log di
    // transazione (escluso: non concorre al tetto dei 10 GB di Express).
    private const string SqlDimensioneDati = """
        SELECT CAST(ISNULL(SUM(CAST(size AS BIGINT)), 0) * 8 / 1024 AS INT)
        FROM sys.database_files
        WHERE type = 0;
        """;

    public async Task<int> GetDimensioneDatiMbAsync(CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(SqlDimensioneDati, cancellationToken: cancellationToken));
    }
}
