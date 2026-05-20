using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Repositories;

/// <summary>
/// Implementazione Dapper di <see cref="IUtenteRepository"/>.
/// Le query SQL sono inline come stringhe const private per:
///   - mantenere il file autocontenuto (zero risorse esterne);
///   - permettere la diff revision di sintassi SQL senza saltare file;
///   - evitare la dispersione di query nei layer superiori (regola di
///     architettura: SQL solo nei repository).
/// </summary>
internal sealed class UtenteRepository : IUtenteRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public UtenteRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    // ---------------------------------------------------------------------
    // Query: estrazione singolo utente
    // ---------------------------------------------------------------------

    private const string SqlSelectByUsername = """
        SELECT
            IdUtente, Username, PasswordHash, PasswordSalt, NomeCompleto,
            Email, Attivo, TemaPreferito, DataRecord, UltimoLoginUtc
        FROM dbo.Utenti
        WHERE Username = @Username;
        """;

    public async Task<Utente?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(
            commandText: SqlSelectByUsername,
            parameters: new { Username = username },
            cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Utente>(cmd);
    }

    private const string SqlSelectById = """
        SELECT
            IdUtente, Username, PasswordHash, PasswordSalt, NomeCompleto,
            Email, Attivo, TemaPreferito, DataRecord, UltimoLoginUtc
        FROM dbo.Utenti
        WHERE IdUtente = @IdUtente;
        """;

    public async Task<Utente?> GetByIdAsync(int idUtente, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(
            commandText: SqlSelectById,
            parameters: new { IdUtente = idUtente },
            cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Utente>(cmd);
    }

    // ---------------------------------------------------------------------
    // Comando: inserimento (usato dal seed dev-only e in futuro da
    // un'eventuale UI di gestione utenti)
    // ---------------------------------------------------------------------

    private const string SqlInsert = """
        INSERT INTO dbo.Utenti
            (Username, PasswordHash, PasswordSalt, NomeCompleto, Email,
             Attivo, TemaPreferito)
        OUTPUT INSERTED.IdUtente
        VALUES
            (@Username, @PasswordHash, @PasswordSalt, @NomeCompleto, @Email,
             @Attivo, @TemaPreferito);
        """;

    public async Task<int> InsertAsync(Utente utente, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(
            commandText: SqlInsert,
            parameters: new
            {
                utente.Username,
                utente.PasswordHash,
                utente.PasswordSalt,
                utente.NomeCompleto,
                utente.Email,
                utente.Attivo,
                utente.TemaPreferito,
            },
            cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(cmd);
    }

    // ---------------------------------------------------------------------
    // Comando: aggiornamento ultimo login
    // ---------------------------------------------------------------------

    private const string SqlUpdateUltimoLogin = """
        UPDATE dbo.Utenti
        SET UltimoLoginUtc = @IstanteUtc
        WHERE IdUtente = @IdUtente;
        """;

    public async Task UpdateUltimoLoginAsync(int idUtente, DateTime istanteUtc, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(
            commandText: SqlUpdateUltimoLogin,
            parameters: new { IdUtente = idUtente, IstanteUtc = istanteUtc },
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(cmd);
    }

    // ---------------------------------------------------------------------
    // Comando: aggiornamento preferenza tema
    // ---------------------------------------------------------------------

    private const string SqlUpdateTemaPreferito = """
        UPDATE dbo.Utenti
        SET TemaPreferito = @TemaPreferito
        WHERE IdUtente = @IdUtente;
        """;

    public async Task UpdateTemaPreferitoAsync(int idUtente, string temaPreferito, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(
            commandText: SqlUpdateTemaPreferito,
            parameters: new { IdUtente = idUtente, TemaPreferito = temaPreferito },
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(cmd);
    }
}
