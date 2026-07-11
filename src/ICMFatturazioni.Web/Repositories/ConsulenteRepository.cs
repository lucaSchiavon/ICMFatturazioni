using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Repositories;

/// <summary>
/// Implementazione Dapper di <see cref="IConsulenteRepository"/> su fatt.Consulenti
/// (migration 077). La colonna SQL <c>Consulente</c> viene aliasata come
/// <c>Descrizione</c> in SELECT per allinearsi alla proprietà dell'entità
/// (CS0542: proprietà e classe non possono avere lo stesso nome), stesso
/// pattern di <see cref="TipoAttivitaRepository"/>.
/// </summary>
internal sealed class ConsulenteRepository : IConsulenteRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public ConsulenteRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    private const string SqlSelect = """
        SELECT IdConsulente,
               Consulente AS Descrizione,
               IsAttivo
        FROM fatt.Consulenti
        """;

    private const string SqlSelectAttivi = SqlSelect + " WHERE IsAttivo = 1 ORDER BY Consulente;";

    public async Task<IReadOnlyList<Consulente>> GetAttiviAsync(CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlSelectAttivi, cancellationToken: cancellationToken);
        var rows = await conn.QueryAsync<Consulente>(cmd);
        return rows.ToList();
    }

    private const string SqlSelectById = SqlSelect + " WHERE IdConsulente = @IdConsulente;";

    public async Task<Consulente?> GetByIdAsync(Guid idConsulente, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlSelectById, new { IdConsulente = idConsulente }, cancellationToken: cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<Consulente>(cmd);
    }

    private const string SqlExistsDescr = """
        SELECT CASE WHEN EXISTS (
            SELECT 1 FROM fatt.Consulenti
            WHERE IsAttivo = 1
              AND UPPER(Consulente) = UPPER(@Descrizione)
              AND (@EscludiId IS NULL OR IdConsulente <> @EscludiId)
        ) THEN 1 ELSE 0 END;
        """;

    public async Task<bool> ExistsDescrizioneAttivaAsync(string descrizione, Guid? escludiId, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlExistsDescr, new { Descrizione = descrizione, EscludiId = escludiId }, cancellationToken: cancellationToken);
        return await conn.ExecuteScalarAsync<bool>(cmd);
    }

    private const string SqlInsert = """
        INSERT INTO fatt.Consulenti (IdConsulente, Consulente, IsAttivo)
        VALUES (@IdConsulente, @Consulente, @IsAttivo);
        """;

    public async Task InsertAsync(Consulente consulente, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlInsert, ToParams(consulente), cancellationToken: cancellationToken);
        await conn.ExecuteAsync(cmd);
    }

    private const string SqlUpdate = """
        UPDATE fatt.Consulenti SET
            Consulente = @Consulente,
            IsAttivo   = @IsAttivo
        WHERE IdConsulente = @IdConsulente;
        """;

    public async Task UpdateAsync(Consulente consulente, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlUpdate, ToParams(consulente), cancellationToken: cancellationToken);
        await conn.ExecuteAsync(cmd);
    }

    private const string SqlDisattiva = "UPDATE fatt.Consulenti SET IsAttivo = 0 WHERE IdConsulente = @IdConsulente;";

    public async Task DisattivaAsync(Guid idConsulente, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlDisattiva, new { IdConsulente = idConsulente }, cancellationToken: cancellationToken);
        await conn.ExecuteAsync(cmd);
    }

    private const string SqlHasDipendenze = """
        SELECT CASE WHEN EXISTS (
            SELECT 1 FROM fatt.AttivitaConsulenti
            WHERE IdConsulente = @IdConsulente AND IsAttivo = 1
        ) THEN 1 ELSE 0 END;
        """;

    public async Task<bool> HasDipendenzeAsync(Guid idConsulente, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlHasDipendenze, new { IdConsulente = idConsulente }, cancellationToken: cancellationToken);
        return await conn.ExecuteScalarAsync<bool>(cmd);
    }

    // Mappa proprietà C# Descrizione → colonna SQL Consulente.
    private static object ToParams(Consulente c) => new
    {
        c.IdConsulente,
        Consulente = c.Descrizione,
        c.IsAttivo,
    };
}
