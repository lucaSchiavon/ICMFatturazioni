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

    // ---------------------------------------------------------------------
    // CRUD ruoli custom (T3b)
    // ---------------------------------------------------------------------

    public async Task<bool> ExistsNomeAsync(string nome, Guid? escludiIdRuolo = null, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM fatt.Ruoli
                WHERE Ruolo = @Nome AND (@Escludi IS NULL OR IdRuolo <> @Escludi)
            ) THEN 1 ELSE 0 END;
            """;
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            sql, new { Nome = nome, Escludi = escludiIdRuolo }, cancellationToken: cancellationToken));
    }

    public async Task<int> CountUtentiAsync(Guid idRuolo, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM fatt.Utenti WHERE IdRuolo = @IdRuolo;",
            new { IdRuolo = idRuolo }, cancellationToken: cancellationToken));
    }

    public async Task InsertAsync(Ruolo ruolo, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        // I ruoli custom hanno Codice NULL e IsSistema = 0.
        const string sql = """
            INSERT INTO fatt.Ruoli (IdRuolo, Codice, Ruolo, Descrizione, IsSistema, IsAttivo)
            VALUES (@IdRuolo, NULL, @Nome, @Descrizione, 0, @IsAttivo);
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { ruolo.IdRuolo, Nome = ruolo.Nome, ruolo.Descrizione, ruolo.IsAttivo },
            cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(Guid idRuolo, string nome, string? descrizione, bool isAttivo, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        const string sql = """
            UPDATE fatt.Ruoli
            SET Ruolo = @Nome, Descrizione = @Descrizione, IsAttivo = @IsAttivo,
                UpdatedAt = SYSUTCDATETIME()
            WHERE IdRuolo = @IdRuolo;
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            sql, new { IdRuolo = idRuolo, Nome = nome, Descrizione = descrizione, IsAttivo = isAttivo },
            cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid idRuolo, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        // Rimuove prima i mapping di visibilità per ruolo (FK), poi il ruolo.
        const string sql = """
            DELETE FROM fatt.SottoMenuRuolo WHERE IdRuolo = @IdRuolo;
            DELETE FROM fatt.MenuRuolo      WHERE IdRuolo = @IdRuolo;
            DELETE FROM fatt.Ruoli          WHERE IdRuolo = @IdRuolo;
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            sql, new { IdRuolo = idRuolo }, cancellationToken: cancellationToken));
    }
}
