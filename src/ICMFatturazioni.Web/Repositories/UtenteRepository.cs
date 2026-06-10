using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Models;
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

    public async Task<Utente?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        // Email univoca quando valorizzata (indice filtrato); il confronto è
        // case-insensitive grazie alla collation di default della colonna.
        var cmd = new CommandDefinition(
            commandText: SqlSelectColumns + " WHERE Email = @Email;",
            parameters: new { Email = email },
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

    // ---------------------------------------------------------------------
    // Amministrazione utenti (T3)
    // ---------------------------------------------------------------------

    public async Task<IReadOnlyList<UtenteConRuolo>> GetAllConRuoloAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        const string sql = """
            SELECT
                u.IdUtente, u.Username, u.Email, u.IdRuolo,
                r.Ruolo AS RuoloNome, r.Codice AS RuoloCodice, u.Attivo,
                CAST(CASE WHEN u.PasswordHash IS NULL THEN 0 ELSE 1 END AS BIT) AS HaPassword,
                u.UltimoLoginUtc
            FROM fatt.Utenti u
            JOIN fatt.Ruoli r ON u.IdRuolo = r.IdRuolo
            ORDER BY u.Username;
            """;
        var rows = await connection.QueryAsync<UtenteConRuolo>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<bool> ExistsUsernameAsync(string username, Guid? escludiIdUtente = null, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM fatt.Utenti
                WHERE Username = @Username
                  AND (@Escludi IS NULL OR IdUtente <> @Escludi)
            ) THEN 1 ELSE 0 END;
            """;
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            sql, new { Username = username, Escludi = escludiIdUtente }, cancellationToken: cancellationToken));
    }

    public async Task UpdateProfiloAsync(Guid idUtente, string username, string? email, Guid idRuolo, bool attivo, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        const string sql = """
            UPDATE fatt.Utenti
            SET Username = @Username, Email = @Email, IdRuolo = @IdRuolo,
                Attivo = @Attivo, UpdatedAt = SYSUTCDATETIME()
            WHERE IdUtente = @IdUtente;
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            sql, new { IdUtente = idUtente, Username = username, Email = email, IdRuolo = idRuolo, Attivo = attivo },
            cancellationToken: cancellationToken));
    }

    public async Task UpdatePasswordHashAsync(Guid idUtente, string? passwordHash, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        const string sql = """
            UPDATE fatt.Utenti
            SET PasswordHash = @PasswordHash, UpdatedAt = SYSUTCDATETIME()
            WHERE IdUtente = @IdUtente;
            """;
        await connection.ExecuteAsync(new CommandDefinition(
            sql, new { IdUtente = idUtente, PasswordHash = passwordHash }, cancellationToken: cancellationToken));
    }
}
