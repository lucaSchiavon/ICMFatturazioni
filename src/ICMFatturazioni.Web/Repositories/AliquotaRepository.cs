using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Repositories;

/// <summary>Implementazione Dapper di <see cref="IAliquotaRepository"/>.</summary>
internal sealed class AliquotaRepository : IAliquotaRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public AliquotaRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    private const string SqlSelectBase = """
        SELECT IdAliquota, Codice, Descrizione, Valore, IsAttivo
        FROM fatt.Aliquote
        """;

    private const string SqlSelectAttivi =
        SqlSelectBase + " WHERE IsAttivo = 1 ORDER BY Descrizione;";

    public async Task<IReadOnlyList<Aliquota>> GetAttiviAsync(CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd  = new CommandDefinition(SqlSelectAttivi, cancellationToken: ct);
        var rows = await conn.QueryAsync<Aliquota>(cmd);
        return rows.ToList();
    }

    private const string SqlSelectById =
        SqlSelectBase + " WHERE IdAliquota = @IdAliquota;";

    public async Task<Aliquota?> GetByIdAsync(Guid idAliquota, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(SqlSelectById, new { IdAliquota = idAliquota }, cancellationToken: ct);
        return await conn.QuerySingleOrDefaultAsync<Aliquota>(cmd);
    }

    private const string SqlInsert = """
        INSERT INTO fatt.Aliquote (IdAliquota, Codice, Descrizione, Valore, IsAttivo)
        VALUES (@IdAliquota, @Codice, @Descrizione, @Valore, @IsAttivo);
        """;

    public async Task InsertAsync(Aliquota aliquota, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(SqlInsert, ToParams(aliquota), cancellationToken: ct);
        await conn.ExecuteAsync(cmd);
    }

    // Codice non aggiornabile: identità stabile delle aliquote di sistema.
    private const string SqlUpdate = """
        UPDATE fatt.Aliquote SET
            Descrizione = @Descrizione,
            Valore      = @Valore
        WHERE IdAliquota = @IdAliquota;
        """;

    public async Task UpdateAsync(Aliquota aliquota, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(SqlUpdate, ToParams(aliquota), cancellationToken: ct);
        await conn.ExecuteAsync(cmd);
    }

    private const string SqlDisattiva =
        "UPDATE fatt.Aliquote SET IsAttivo = 0 WHERE IdAliquota = @IdAliquota;";

    public async Task DisattivaAsync(Guid idAliquota, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(SqlDisattiva, new { IdAliquota = idAliquota }, cancellationToken: ct);
        await conn.ExecuteAsync(cmd);
    }

    private static object ToParams(Aliquota a) => new
    {
        a.IdAliquota,
        a.Codice,
        a.Descrizione,
        a.Valore,
        a.IsAttivo,
    };
}
