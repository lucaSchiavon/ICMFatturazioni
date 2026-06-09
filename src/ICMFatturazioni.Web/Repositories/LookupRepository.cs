using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Repositories;

/// <summary>
/// Lettura dei lookup <c>fatt.*</c> per i dropdown delle maschere.
/// Stateless, registrato singleton: i lookup non cambiano spesso e
/// vengono ri-letti ad ogni apertura del form (potenzialmente con
/// caching in futuro).
/// </summary>
internal sealed class LookupRepository : ILookupRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public LookupRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    // Italia in cima (ORDER BY (case when ... then 0 else 1 end)): è il
    // default applicativo per le anagrafiche, e mostrarlo in testa
    // riduce gli scroll inutili nei dropdown.
    private const string SqlPaesi = """
        SELECT CodicePaese AS Codice, Paese AS Descrizione
        FROM fatt.Paesi
        ORDER BY CASE WHEN CodicePaese = N'IT' THEN 0 ELSE 1 END, Paese;
        """;

    public async Task<IReadOnlyList<LookupItem>> GetPaesiAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlPaesi, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<LookupItem>(cmd);
        return rows.ToList();
    }

    private const string SqlProvince = """
        SELECT Prov AS Codice, Provincia AS Descrizione
        FROM fatt.Province
        WHERE Provincia IS NOT NULL
        ORDER BY Provincia;
        """;

    public async Task<IReadOnlyList<LookupItem>> GetProvinceAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlProvince, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<LookupItem>(cmd);
        return rows.ToList();
    }
}
