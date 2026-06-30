using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Repositories;

/// <summary>
/// Implementazione Dapper di <see cref="ISpesaAnticipataRepository"/>.
/// Data è una colonna DATE: stessa tecnica di conversione DateTime ↔ DateOnly
/// usata negli altri repository con colonne DATE.
/// </summary>
internal sealed class SpesaAnticipataRepository : ISpesaAnticipataRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public SpesaAnticipataRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    // DTO: Data come DateTime per compatibilità Dapper con DATE SQL.
    private sealed class SpesaAnticipataRow
    {
        public Guid     IdSpesaAnticipata { get; init; }
        public Guid     IdAttivita        { get; init; }
        public DateTime Data              { get; init; }
        public string   Descrizione       { get; init; } = string.Empty;
        public decimal  Importo           { get; init; }
        public bool     IsAttivo          { get; init; }
    }

    private static SpesaAnticipata ToEntity(SpesaAnticipataRow r) => new()
    {
        IdSpesaAnticipata = r.IdSpesaAnticipata,
        IdAttivita        = r.IdAttivita,
        Data              = DateOnly.FromDateTime(r.Data),
        Descrizione       = r.Descrizione,
        Importo           = r.Importo,
        IsAttivo          = r.IsAttivo,
    };

    // Data: DATE NOT NULL in SQL → DateTime in params.
    private static DateTime ToSqlDate(DateOnly d) => d.ToDateTime(TimeOnly.MinValue);

    private const string SqlSelectBase = """
        SELECT IdSpesaAnticipata, IdAttivita, Data, Descrizione, Importo, IsAttivo
        FROM fatt.SpeseAnticipate
        """;

    private const string SqlSelectByAttivita =
        SqlSelectBase + " WHERE IdAttivita = @IdAttivita AND IsAttivo = 1 ORDER BY Data ASC;";

    public async Task<IReadOnlyList<SpesaAnticipata>> GetByAttivitaAsync(Guid idAttivita, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd  = new CommandDefinition(SqlSelectByAttivita, new { IdAttivita = idAttivita }, cancellationToken: ct);
        var rows = await conn.QueryAsync<SpesaAnticipataRow>(cmd);
        return rows.Select(ToEntity).ToList();
    }

    private const string SqlSelectById =
        SqlSelectBase + " WHERE IdSpesaAnticipata = @IdSpesaAnticipata;";

    public async Task<SpesaAnticipata?> GetByIdAsync(Guid idSpesaAnticipata, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(SqlSelectById, new { IdSpesaAnticipata = idSpesaAnticipata }, cancellationToken: ct);
        var row = await conn.QuerySingleOrDefaultAsync<SpesaAnticipataRow>(cmd);
        return row is null ? null : ToEntity(row);
    }

    private const string SqlInsert = """
        INSERT INTO fatt.SpeseAnticipate
            (IdSpesaAnticipata, IdAttivita, Data, Descrizione, Importo, IsAttivo)
        VALUES
            (@IdSpesaAnticipata, @IdAttivita, @Data, @Descrizione, @Importo, @IsAttivo);
        """;

    public async Task InsertAsync(SpesaAnticipata spesa, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(SqlInsert, ToParams(spesa), cancellationToken: ct);
        await conn.ExecuteAsync(cmd);
    }

    private const string SqlUpdate = """
        UPDATE fatt.SpeseAnticipate SET
            Data        = @Data,
            Descrizione = @Descrizione,
            Importo     = @Importo
        WHERE IdSpesaAnticipata = @IdSpesaAnticipata;
        """;

    public async Task UpdateAsync(SpesaAnticipata spesa, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(SqlUpdate, ToParams(spesa), cancellationToken: ct);
        await conn.ExecuteAsync(cmd);
    }

    private const string SqlDisattiva =
        "UPDATE fatt.SpeseAnticipate SET IsAttivo = 0 WHERE IdSpesaAnticipata = @IdSpesaAnticipata;";

    public async Task DisattivaAsync(Guid idSpesaAnticipata, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(SqlDisattiva, new { IdSpesaAnticipata = idSpesaAnticipata }, cancellationToken: ct);
        await conn.ExecuteAsync(cmd);
    }

    private static object ToParams(SpesaAnticipata s) => new
    {
        s.IdSpesaAnticipata,
        s.IdAttivita,
        Data = ToSqlDate(s.Data),
        s.Descrizione,
        s.Importo,
        s.IsAttivo,
    };
}
