using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Repositories;

/// <summary>
/// Implementazione Dapper di <see cref="IErrorLogRepository"/>.
/// L'INSERT è protetto da try/catch: qualsiasi errore (connessione,
/// timeout, deadlock) è silenziato verso il chiamante e segnalato
/// solo dal valore di ritorno <c>false</c>. Il fallback testuale su
/// file è responsabilità del livello superiore (<c>ErrorLogger</c>).
/// </summary>
internal sealed class ErrorLogRepository : IErrorLogRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public ErrorLogRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private const string SqlInsert = """
        INSERT INTO dbo.LogErrors
            (TimestampUtc, ExceptionType, [Message], StackTrace,
             InnerExceptionType, InnerExceptionMessage, InnerExceptionStackTrace,
             [Source], DescrizioneEstesa, Contesto, UserId, UserName, RequestPath,
             MachineName, EnvironmentName, CorrelationId, Severity, Handled)
        VALUES
            (@TimestampUtc, @ExceptionType, @Message, @StackTrace,
             @InnerExceptionType, @InnerExceptionMessage, @InnerExceptionStackTrace,
             @Source, @DescrizioneEstesa, @Contesto, @UserId, @UserName, @RequestPath,
             @MachineName, @EnvironmentName, @CorrelationId, @Severity, @Handled);
        """;

    public async Task<bool> InsertAsync(LogError entry, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            var cmd = new CommandDefinition(
                commandText: SqlInsert,
                parameters: new
                {
                    entry.TimestampUtc,
                    entry.ExceptionType,
                    entry.Message,
                    entry.StackTrace,
                    entry.InnerExceptionType,
                    entry.InnerExceptionMessage,
                    entry.InnerExceptionStackTrace,
                    entry.Source,
                    entry.DescrizioneEstesa,
                    entry.Contesto,
                    entry.UserId,
                    entry.UserName,
                    entry.RequestPath,
                    entry.MachineName,
                    entry.EnvironmentName,
                    entry.CorrelationId,
                    Severity = (byte)entry.Severity,
                    entry.Handled,
                },
                cancellationToken: cancellationToken);
            await connection.ExecuteAsync(cmd);
            return true;
        }
        catch
        {
            // Volutamente silenzioso: il logger superiore farà fallback su
            // file. Mai propagare un errore di logging.
            return false;
        }
    }
}
