using System.Data;
using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Repositories;

/// <summary>
/// Implementazione Dapper di <see cref="IUtenteTokenRepository"/> su
/// <c>fatt.UtenteToken</c>. Le operazioni multi-statement girano in transazione
/// (la nostra <see cref="ISqlConnectionFactory"/> espone <see cref="IDbConnection"/>,
/// quindi si usa <see cref="IDbConnection.BeginTransaction()"/> sincrono).
/// </summary>
internal sealed class UtenteTokenRepository : IUtenteTokenRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public UtenteTokenRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    // Revoca i token "attivi" dello stesso utente+tipo. UsatoUtc IS NULL preserva
    // quelli già consumati; RevocatoUtc IS NULL rende l'operazione idempotente.
    private const string SqlRevocaAttivi = """
        UPDATE fatt.UtenteToken
        SET RevocatoUtc = SYSUTCDATETIME()
        WHERE UtenteId = @UtenteId
          AND Tipo = @Tipo
          AND UsatoUtc IS NULL
          AND RevocatoUtc IS NULL;
        """;

    private const string SqlInsert = """
        INSERT INTO fatt.UtenteToken (Id, UtenteId, TokenHash, Tipo, ScadenzaUtc, UsatoUtc, RevocatoUtc, CreatedAt)
        VALUES (@Id, @UtenteId, @TokenHash, @Tipo, @ScadenzaUtc, NULL, NULL, @CreatedAt);
        """;

    private const string SqlGetByHash = """
        SELECT Id, UtenteId, TokenHash, Tipo, ScadenzaUtc, UsatoUtc, RevocatoUtc, CreatedAt
        FROM fatt.UtenteToken
        WHERE TokenHash = @TokenHash;
        """;

    // Sentinel TOCTOU: marca usato solo se ancora utilizzabile. @@ROWCOUNT = 0
    // se il token è stato consumato/revocato/scaduto nel frattempo.
    private const string SqlMarkUsato = """
        UPDATE fatt.UtenteToken
        SET UsatoUtc = SYSUTCDATETIME()
        WHERE Id = @Id
          AND UsatoUtc IS NULL
          AND RevocatoUtc IS NULL
          AND ScadenzaUtc > SYSUTCDATETIME();
        """;

    private const string SqlSetPassword = """
        UPDATE fatt.Utenti
        SET PasswordHash = @PasswordHash,
            UpdatedAt = SYSUTCDATETIME()
        WHERE IdUtente = @UtenteId;
        """;

    public async Task CreaRevocandoPrecedentiAsync(UtenteToken nuovo, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = conn.BeginTransaction();
        try
        {
            await conn.ExecuteAsync(new CommandDefinition(SqlRevocaAttivi,
                new { nuovo.UtenteId, Tipo = (byte)nuovo.Tipo },
                transaction: tx, cancellationToken: cancellationToken));

            await conn.ExecuteAsync(new CommandDefinition(SqlInsert, new
            {
                nuovo.Id,
                nuovo.UtenteId,
                nuovo.TokenHash,
                Tipo = (byte)nuovo.Tipo,
                nuovo.ScadenzaUtc,
                nuovo.CreatedAt,
            }, transaction: tx, cancellationToken: cancellationToken));

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<UtenteToken?> GetByHashAsync(byte[] tokenHash, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<UtenteToken>(
            new CommandDefinition(SqlGetByHash, new { TokenHash = tokenHash }, cancellationToken: cancellationToken));
    }

    public async Task<int> ConsumaEImpostaPasswordAsync(Guid tokenId, Guid utenteId, string passwordHash, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = conn.BeginTransaction();
        try
        {
            var righe = await conn.ExecuteAsync(new CommandDefinition(SqlMarkUsato,
                new { Id = tokenId }, transaction: tx, cancellationToken: cancellationToken));

            // Difesa sotto race: se il token non era più utilizzabile non si
            // tocca la password. La transazione si chiude senza effetti.
            if (righe == 0)
            {
                tx.Rollback();
                return 0;
            }

            await conn.ExecuteAsync(new CommandDefinition(SqlSetPassword,
                new { UtenteId = utenteId, PasswordHash = passwordHash },
                transaction: tx, cancellationToken: cancellationToken));

            tx.Commit();
            return righe;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}
