using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Repositories;

/// <summary>
/// Implementazione Dapper di <see cref="IUtenteRepository"/>. Le query SQL
/// sono inline come stringhe const private (regola: SQL solo nei repository).
/// </summary>
internal sealed class UtenteRepository : IUtenteRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public UtenteRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    // Colonne centralizzate: GetByUsername e GetById leggono lo stesso set.
    private const string SqlSelectColumns = """
        SELECT
            IdUtente, Username, Email, PasswordHash, IdRuolo, NomeCompleto,
            Attivo, TemaPreferito, UltimoLoginUtc, CreatedAt, UpdatedAt
        FROM fatt.Utenti
        """;

    public async Task<Utente?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(
            commandText: SqlSelectColumns + " WHERE Username = @Username;",
            parameters: new { Username = username },
            cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Utente>(cmd);
    }

    public async Task<Utente?> GetByIdAsync(Guid idUtente, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(
            commandText: SqlSelectColumns + " WHERE IdUtente = @IdUtente;",
            parameters: new { IdUtente = idUtente },
            cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Utente>(cmd);
    }

    // IdUtente generato app-side (GUID v7); CreatedAt/UpdatedAt dai DEFAULT del DB.
    private const string SqlInsert = """
        INSERT INTO fatt.Utenti
            (IdUtente, Username, Email, PasswordHash, IdRuolo, NomeCompleto,
             Attivo, TemaPreferito)
        VALUES
            (@IdUtente, @Username, @Email, @PasswordHash, @IdRuolo, @NomeCompleto,
             @Attivo, @TemaPreferito);
        """;

    public async Task InsertAsync(Utente utente, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(
            commandText: SqlInsert,
            parameters: new
            {
                utente.IdUtente,
                utente.Username,
                utente.Email,
                utente.PasswordHash,
                utente.IdRuolo,
                utente.NomeCompleto,
                utente.Attivo,
                utente.TemaPreferito,
            },
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(cmd);
    }

    private const string SqlUpdateUltimoLogin = """
        UPDATE fatt.Utenti
        SET UltimoLoginUtc = @IstanteUtc, UpdatedAt = SYSUTCDATETIME()
        WHERE IdUtente = @IdUtente;
        """;

    public async Task UpdateUltimoLoginAsync(Guid idUtente, DateTime istanteUtc, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(
            commandText: SqlUpdateUltimoLogin,
            parameters: new { IdUtente = idUtente, IstanteUtc = istanteUtc },
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(cmd);
    }

    private const string SqlUpdateTemaPreferito = """
        UPDATE fatt.Utenti
        SET TemaPreferito = @TemaPreferito, UpdatedAt = SYSUTCDATETIME()
        WHERE IdUtente = @IdUtente;
        """;

    public async Task UpdateTemaPreferitoAsync(Guid idUtente, string temaPreferito, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(
            commandText: SqlUpdateTemaPreferito,
            parameters: new { IdUtente = idUtente, TemaPreferito = temaPreferito },
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(cmd);
    }
}
