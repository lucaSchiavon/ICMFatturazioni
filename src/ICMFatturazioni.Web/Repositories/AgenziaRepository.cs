using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Repositories;

/// <summary>
/// Implementazione Dapper di <see cref="IAgenziaRepository"/> su <c>fatt.Agenzie</c>.
/// </summary>
internal sealed class AgenziaRepository : IAgenziaRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public AgenziaRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private const string SqlSelect = "SELECT IdAgenzia, IdBanca, Nome, CAB, IsAttivo FROM fatt.Agenzie";

    private const string SqlSelectByBanca = SqlSelect + " WHERE IsAttivo = 1 AND IdBanca = @IdBanca ORDER BY Nome;";

    public async Task<IReadOnlyList<Agenzia>> GetByBancaAttiveAsync(Guid idBanca, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlSelectByBanca, new { IdBanca = idBanca }, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<Agenzia>(cmd);
        return rows.ToList();
    }

    private const string SqlSelectById = SqlSelect + " WHERE IdAgenzia = @IdAgenzia;";

    public async Task<Agenzia?> GetByIdAsync(Guid idAgenzia, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlSelectById, new { IdAgenzia = idAgenzia }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Agenzia>(cmd);
    }

    private const string SqlSelectByNome = SqlSelect + " WHERE IsAttivo = 1 AND IdBanca = @IdBanca AND UPPER(Nome) = UPPER(@Nome);";

    public async Task<Agenzia?> GetByNomeAttivaAsync(Guid idBanca, string nome, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlSelectByNome, new { IdBanca = idBanca, Nome = nome }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Agenzia>(cmd);
    }

    private const string SqlInsert = """
        INSERT INTO fatt.Agenzie (IdAgenzia, IdBanca, Nome, CAB, IsAttivo)
        VALUES (@IdAgenzia, @IdBanca, @Nome, @CAB, @IsAttivo);
        """;

    public async Task InsertAsync(Agenzia agenzia, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlInsert,
            new { agenzia.IdAgenzia, agenzia.IdBanca, agenzia.Nome, agenzia.CAB, agenzia.IsAttivo },
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(cmd);
    }

    private const string SqlUpdate = """
        UPDATE fatt.Agenzie SET Nome = @Nome, CAB = @CAB, IsAttivo = @IsAttivo
        WHERE IdAgenzia = @IdAgenzia;
        """;

    public async Task UpdateAsync(Agenzia agenzia, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlUpdate,
            new { agenzia.IdAgenzia, agenzia.Nome, agenzia.CAB, agenzia.IsAttivo },
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(cmd);
    }
}
