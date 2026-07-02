using System.Text;
using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Models;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Repositories;

/// <summary>
/// Implementazione Dapper di <see cref="IAuditRepository"/> su <c>fatt.Audit</c>.
/// </summary>
internal sealed class AuditRepository : IAuditRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public AuditRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    private const string SqlInsert = """
        INSERT INTO fatt.Audit
            (Id, TimestampUtc, UtenteId, UtenteNome, Operazione, EntityType, EntityId, Descrizione, Dati)
        VALUES
            (@Id, @TimestampUtc, @UtenteId, @UtenteNome, @Operazione, @EntityType, @EntityId, @Descrizione, @Dati);
        """;

    public async Task InsertAsync(Audit entry, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(new CommandDefinition(SqlInsert, new
        {
            entry.Id,
            entry.TimestampUtc,
            entry.UtenteId,
            entry.UtenteNome,
            Operazione = (byte)entry.Operazione,
            entry.EntityType,
            entry.EntityId,
            entry.Descrizione,
            entry.Dati,
        }, cancellationToken: cancellationToken));
    }

    // Colonne selezionate (condivise da ricerca ed export).
    private const string ColonneSelect =
        "Id, TimestampUtc, UtenteId, UtenteNome, Operazione, EntityType, EntityId, Descrizione, Dati";

    // Costruzione del predicato WHERE + parametri, condivisa da CercaAsync (paginata)
    // ed EsportaAsync (tutte le righe): un solo punto di verità sui filtri.
    private static (StringBuilder Where, DynamicParameters P) CostruisciFiltro(AuditFiltro filtro)
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
        if (filtro.Operazione is { } op)
        {
            where.Append(" AND Operazione = @Operazione");
            p.Add("Operazione", (byte)op);
        }
        if (!string.IsNullOrWhiteSpace(filtro.EntityType))
        {
            where.Append(" AND EntityType = @EntityType");
            p.Add("EntityType", filtro.EntityType);
        }
        if (!string.IsNullOrWhiteSpace(filtro.Testo))
        {
            where.Append(" AND (UtenteNome LIKE @Testo OR Descrizione LIKE @Testo)");
            p.Add("Testo", $"%{SqlRicerca.EscapeLike(filtro.Testo)}%");
        }

        return (where, p);
    }

    public async Task<AuditRisultato> CercaAsync(AuditFiltro filtro, CancellationToken cancellationToken = default)
    {
        var (where, p) = CostruisciFiltro(filtro);

        var (pagina, dimensione) = SqlRicerca.NormalizzaPaginazione(filtro.Pagina, filtro.Dimensione);
        p.Add("Offset", (pagina - 1) * dimensione);
        p.Add("Limit", dimensione);

        var sql = $"""
            SELECT COUNT(*) FROM fatt.Audit {where};

            SELECT {ColonneSelect}
            FROM fatt.Audit
            {where}
            ORDER BY TimestampUtc DESC
            OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY;
            """;

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var grid = await conn.QueryMultipleAsync(new CommandDefinition(sql, p, cancellationToken: cancellationToken));
        var totale = await grid.ReadSingleAsync<int>();
        var righe = (await grid.ReadAsync<Audit>()).ToList();
        return new AuditRisultato(righe, totale);
    }

    public async Task<IReadOnlyList<Audit>> EsportaAsync(AuditFiltro filtro, int maxRighe, CancellationToken cancellationToken = default)
    {
        var (where, p) = CostruisciFiltro(filtro);
        // Tetto di sicurezza applicato in SQL con TOP: NON passa dal cap dei 200
        // della paginazione (quello serve alla griglia), qui servono TUTTE le righe.
        p.Add("Limit", maxRighe < 1 ? 1 : maxRighe);

        var sql = $"""
            SELECT TOP (@Limit) {ColonneSelect}
            FROM fatt.Audit
            {where}
            ORDER BY TimestampUtc DESC;
            """;

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var righe = await conn.QueryAsync<Audit>(new CommandDefinition(sql, p, cancellationToken: cancellationToken));
        return righe.ToList();
    }

    public async Task<IReadOnlyList<string>> GetEntityTypesAsync(CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<string>(new CommandDefinition(
            "SELECT DISTINCT EntityType FROM fatt.Audit ORDER BY EntityType;", cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<int> PurgaPrecedentiAsync(DateTime sogliaUtc, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM fatt.Audit WHERE TimestampUtc < @SogliaUtc;",
            new { SogliaUtc = sogliaUtc }, cancellationToken: cancellationToken));
    }
}
