using System.Data;
using System.Text;
using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Models;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Repositories;

/// <summary>
/// Implementazione Dapper di <see cref="ILogRepository"/> su <c>fatt.Log</c>.
/// Singleton (stateless, dipende solo dalla <see cref="ISqlConnectionFactory"/>):
/// è usata dal <c>LogWriterService</c> (BackgroundService singleton) e dal
/// <c>LogManager</c> scoped.
/// </summary>
internal sealed class LogRepository : ILogRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public LogRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    private const string SqlInsert = """
        INSERT INTO fatt.Log
            (Id, TimestampUtc, Livello, Sorgente, Messaggio, EccezioneTipo, StackTrace,
             SpiegazioneUtente, RequestId, UtenteId, EntityId, EntityType)
        VALUES
            (@Id, @TimestampUtc, @Livello, @Sorgente, @Messaggio, @EccezioneTipo, @StackTrace,
             @SpiegazioneUtente, @RequestId, @UtenteId, @EntityId, @EntityType);
        """;

    public async Task InsertAsync(Log entry, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(new CommandDefinition(SqlInsert, ToParam(entry), cancellationToken: cancellationToken));
    }

    public async Task InsertBatchAsync(IReadOnlyList<Log> entries, CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0)
        {
            return;
        }

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        // Dapper esegue l'INSERT una volta per ogni elemento dell'enumerable.
        await conn.ExecuteAsync(new CommandDefinition(
            SqlInsert, entries.Select(ToParam), cancellationToken: cancellationToken));
    }

    public async Task<LogRisultato> CercaAsync(LogFiltro filtro, CancellationToken cancellationToken = default)
    {
        var where = new StringBuilder("WHERE 1 = 1");
        var p = new DynamicParameters();

        if (filtro.DaUtc is { } da)
        {
            where.Append(" AND TimestampUtc >= @DaUtc");
            p.Add("DaUtc", da);
        }
        if (filtro.AUtc is { } a)
        {
            where.Append(" AND TimestampUtc < @AUtc");
            p.Add("AUtc", a);
        }
        if (filtro.Livello is { } liv)
        {
            where.Append(" AND Livello = @Livello");
            p.Add("Livello", (byte)liv);
        }
        if (!string.IsNullOrWhiteSpace(filtro.SorgenteContiene))
        {
            where.Append(" AND Sorgente LIKE @Sorgente");
            p.Add("Sorgente", $"%{SqlRicerca.EscapeLike(filtro.SorgenteContiene)}%");
        }
        if (!string.IsNullOrWhiteSpace(filtro.Testo))
        {
            where.Append(" AND (Messaggio LIKE @Testo OR SpiegazioneUtente LIKE @Testo)");
            p.Add("Testo", $"%{SqlRicerca.EscapeLike(filtro.Testo)}%");
        }

        var (pagina, dimensione) = SqlRicerca.NormalizzaPaginazione(filtro.Pagina, filtro.Dimensione);
        p.Add("Offset", (pagina - 1) * dimensione);
        p.Add("Limit", dimensione);

        var sql = $"""
            SELECT COUNT(*) FROM fatt.Log {where};

            SELECT Id, TimestampUtc, Livello, Sorgente, Messaggio, EccezioneTipo, StackTrace,
                   SpiegazioneUtente, RequestId, UtenteId, EntityId, EntityType
            FROM fatt.Log
            {where}
            ORDER BY TimestampUtc DESC
            OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY;
            """;

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var grid = await conn.QueryMultipleAsync(new CommandDefinition(sql, p, cancellationToken: cancellationToken));
        var totale = await grid.ReadSingleAsync<int>();
        var righe = (await grid.ReadAsync<Log>()).ToList();
        return new LogRisultato(righe, totale);
    }

    public async Task<int> PurgaPrecedentiAsync(DateTime sogliaUtc, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM fatt.Log WHERE TimestampUtc < @SogliaUtc;",
            new { SogliaUtc = sogliaUtc }, cancellationToken: cancellationToken));
    }

    // Proietta l'entità sui parametri Dapper, convertendo l'enum a byte per il
    // TINYINT della colonna Livello.
    private static object ToParam(Log e) => new
    {
        e.Id,
        e.TimestampUtc,
        Livello = (byte)e.Livello,
        e.Sorgente,
        e.Messaggio,
        e.EccezioneTipo,
        e.StackTrace,
        e.SpiegazioneUtente,
        e.RequestId,
        e.UtenteId,
        e.EntityId,
        e.EntityType,
    };
}
