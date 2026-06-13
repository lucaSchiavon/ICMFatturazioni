using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Repositories;

/// <summary>
/// Implementazione Dapper di <see cref="IBancaRepository"/> su <c>fatt.Banche</c>.
/// </summary>
internal sealed class BancaRepository : IBancaRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public BancaRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private const string SqlSelect = "SELECT IdBanca, Nome, ABI, IsAttivo FROM fatt.Banche";

    private const string SqlSelectAttive = SqlSelect + " WHERE IsAttivo = 1 ORDER BY Nome;";

    public async Task<IReadOnlyList<Banca>> GetAttiveAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlSelectAttive, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<Banca>(cmd);
        return rows.ToList();
    }

    private const string SqlSelectById = SqlSelect + " WHERE IdBanca = @IdBanca;";

    public async Task<Banca?> GetByIdAsync(Guid idBanca, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlSelectById, new { IdBanca = idBanca }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Banca>(cmd);
    }

    // Confronto case-insensitive esplicito (la collation di default è CI, ma
    // rendiamo l'intenzione indipendente dall'ambiente).
    private const string SqlSelectByNome = SqlSelect + " WHERE IsAttivo = 1 AND UPPER(Nome) = UPPER(@Nome);";

    public async Task<Banca?> GetByNomeAttivaAsync(string nome, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlSelectByNome, new { Nome = nome }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Banca>(cmd);
    }

    private const string SqlInsert = """
        INSERT INTO fatt.Banche (IdBanca, Nome, ABI, IsAttivo)
        VALUES (@IdBanca, @Nome, @ABI, @IsAttivo);
        """;

    public async Task InsertAsync(Banca banca, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlInsert,
            new { banca.IdBanca, banca.Nome, banca.ABI, banca.IsAttivo },
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(cmd);
    }

    private const string SqlUpdate = """
        UPDATE fatt.Banche SET Nome = @Nome, ABI = @ABI, IsAttivo = @IsAttivo
        WHERE IdBanca = @IdBanca;
        """;

    public async Task UpdateAsync(Banca banca, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlUpdate,
            new { banca.IdBanca, banca.Nome, banca.ABI, banca.IsAttivo },
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(cmd);
    }
}
