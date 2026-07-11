using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Repositories;

/// <summary>
/// Implementazione Dapper di <see cref="ITipoAttivitaConsulenteRepository"/> su
/// fatt.TipiAttivitaConsulenti (migration 077). La colonna SQL
/// <c>TipoAttivitaConsulente</c> viene aliasata come <c>Descrizione</c> in SELECT
/// per allinearsi alla proprietà dell'entità (CS0542), stesso pattern di
/// <see cref="TipoAttivitaRepository"/>.
/// </summary>
internal sealed class TipoAttivitaConsulenteRepository : ITipoAttivitaConsulenteRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public TipoAttivitaConsulenteRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    private const string SqlSelect = """
        SELECT IdTipoAttivitaConsulente,
               TipoAttivitaConsulente AS Descrizione,
               IsAttivo
        FROM fatt.TipiAttivitaConsulenti
        """;

    private const string SqlSelectAttivi = SqlSelect + " WHERE IsAttivo = 1 ORDER BY TipoAttivitaConsulente;";

    public async Task<IReadOnlyList<TipoAttivitaConsulente>> GetAttiviAsync(CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlSelectAttivi, cancellationToken: cancellationToken);
        var rows = await conn.QueryAsync<TipoAttivitaConsulente>(cmd);
        return rows.ToList();
    }

    private const string SqlSelectById = SqlSelect + " WHERE IdTipoAttivitaConsulente = @IdTipoAttivitaConsulente;";

    public async Task<TipoAttivitaConsulente?> GetByIdAsync(Guid idTipoAttivitaConsulente, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlSelectById, new { IdTipoAttivitaConsulente = idTipoAttivitaConsulente }, cancellationToken: cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<TipoAttivitaConsulente>(cmd);
    }

    private const string SqlExistsDescr = """
        SELECT CASE WHEN EXISTS (
            SELECT 1 FROM fatt.TipiAttivitaConsulenti
            WHERE IsAttivo = 1
              AND UPPER(TipoAttivitaConsulente) = UPPER(@Descrizione)
              AND (@EscludiId IS NULL OR IdTipoAttivitaConsulente <> @EscludiId)
        ) THEN 1 ELSE 0 END;
        """;

    public async Task<bool> ExistsDescrizioneAttivaAsync(string descrizione, Guid? escludiId, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlExistsDescr, new { Descrizione = descrizione, EscludiId = escludiId }, cancellationToken: cancellationToken);
        return await conn.ExecuteScalarAsync<bool>(cmd);
    }

    private const string SqlInsert = """
        INSERT INTO fatt.TipiAttivitaConsulenti (IdTipoAttivitaConsulente, TipoAttivitaConsulente, IsAttivo)
        VALUES (@IdTipoAttivitaConsulente, @TipoAttivitaConsulente, @IsAttivo);
        """;

    public async Task InsertAsync(TipoAttivitaConsulente tipo, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlInsert, ToParams(tipo), cancellationToken: cancellationToken);
        await conn.ExecuteAsync(cmd);
    }

    private const string SqlUpdate = """
        UPDATE fatt.TipiAttivitaConsulenti SET
            TipoAttivitaConsulente = @TipoAttivitaConsulente,
            IsAttivo               = @IsAttivo
        WHERE IdTipoAttivitaConsulente = @IdTipoAttivitaConsulente;
        """;

    public async Task UpdateAsync(TipoAttivitaConsulente tipo, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlUpdate, ToParams(tipo), cancellationToken: cancellationToken);
        await conn.ExecuteAsync(cmd);
    }

    private const string SqlDisattiva = "UPDATE fatt.TipiAttivitaConsulenti SET IsAttivo = 0 WHERE IdTipoAttivitaConsulente = @IdTipoAttivitaConsulente;";

    public async Task DisattivaAsync(Guid idTipoAttivitaConsulente, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlDisattiva, new { IdTipoAttivitaConsulente = idTipoAttivitaConsulente }, cancellationToken: cancellationToken);
        await conn.ExecuteAsync(cmd);
    }

    private const string SqlHasDipendenze = """
        SELECT CASE WHEN EXISTS (
            SELECT 1 FROM fatt.AttivitaConsulenti
            WHERE IdTipoAttivitaConsulente = @IdTipoAttivitaConsulente AND IsAttivo = 1
        ) THEN 1 ELSE 0 END;
        """;

    public async Task<bool> HasDipendenzeAsync(Guid idTipoAttivitaConsulente, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlHasDipendenze, new { IdTipoAttivitaConsulente = idTipoAttivitaConsulente }, cancellationToken: cancellationToken);
        return await conn.ExecuteScalarAsync<bool>(cmd);
    }

    // Mappa proprietà C# Descrizione → colonna SQL TipoAttivitaConsulente.
    private static object ToParams(TipoAttivitaConsulente t) => new
    {
        t.IdTipoAttivitaConsulente,
        TipoAttivitaConsulente = t.Descrizione,
        t.IsAttivo,
    };
}
