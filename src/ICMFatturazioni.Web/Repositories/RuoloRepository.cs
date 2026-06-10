using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Repositories;

/// <summary>
/// Implementazione Dapper di <see cref="IRuoloRepository"/>.
/// </summary>
/// <remarks>
/// La colonna SQL si chiama <c>Ruolo</c> ma la proprietà dell'entità è
/// <see cref="Ruolo.Nome"/> (non si può avere una proprietà omonima della
/// classe che la contiene): la SELECT la rinomina con <c>Ruolo AS Nome</c>.
/// </remarks>
internal sealed class RuoloRepository : IRuoloRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public RuoloRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private const string SqlSelectColumns = """
        SELECT
            IdRuolo, Codice, Ruolo AS Nome, Descrizione, IsSistema, IsAttivo,
            CreatedAt, UpdatedAt
        FROM fatt.Ruoli
        """;

    public async Task<IReadOnlyList<Ruolo>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        // IsSistema DESC: i ruoli di sistema (Superadmin, Admin) in cima.
        var sql = SqlSelectColumns + " ORDER BY IsSistema DESC, Ruolo;";
        var cmd = new CommandDefinition(sql, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<Ruolo>(cmd);
        return rows.ToList();
    }

    public async Task<Ruolo?> GetByIdAsync(Guid idRuolo, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(
            SqlSelectColumns + " WHERE IdRuolo = @IdRuolo;",
            parameters: new { IdRuolo = idRuolo },
            cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Ruolo>(cmd);
    }

    public async Task<Ruolo?> GetByCodiceAsync(string codice, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(
            SqlSelectColumns + " WHERE Codice = @Codice;",
            parameters: new { Codice = codice },
            cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Ruolo>(cmd);
    }
}
