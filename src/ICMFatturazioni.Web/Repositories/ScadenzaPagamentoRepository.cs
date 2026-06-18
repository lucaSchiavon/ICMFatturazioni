using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Repositories;

/// <summary>
/// Implementazione Dapper di <see cref="IScadenzaPagamentoRepository"/>.
/// DataScadenza è una colonna DATE: stessa tecnica di conversione
/// DateTime? ↔ DateOnly usata negli altri repository con colonne DATE.
/// </summary>
internal sealed class ScadenzaPagamentoRepository : IScadenzaPagamentoRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public ScadenzaPagamentoRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    // DTO: DataScadenza come DateTime? per compatibilità Dapper con DATE SQL.
    private sealed class ScadenzaPagamentoRow
    {
        public Guid     IdScadenza          { get; init; }
        public Guid     IdAttivitaDettaglio { get; init; }
        public DateTime DataScadenza        { get; init; }
        public decimal  Importo             { get; init; }
        public string?  Nota               { get; init; }
        public bool     IsAttivo            { get; init; }
    }

    private static ScadenzaPagamento ToEntity(ScadenzaPagamentoRow r) => new()
    {
        IdScadenza          = r.IdScadenza,
        IdAttivitaDettaglio = r.IdAttivitaDettaglio,
        DataScadenza        = DateOnly.FromDateTime(r.DataScadenza),
        Importo             = r.Importo,
        Nota                = r.Nota,
        IsAttivo            = r.IsAttivo,
    };

    // DataScadenza: DATE NOT NULL in SQL → DateTime in ToEntity, DateTime? in params.
    private static DateTime? ToSqlDate(DateOnly d)
        => d.ToDateTime(TimeOnly.MinValue);

    private const string SqlSelectBase = """
        SELECT IdScadenza, IdAttivitaDettaglio, DataScadenza, Importo, Nota, IsAttivo
        FROM fatt.SchedulazionePagamenti
        """;

    private const string SqlSelectByDettaglio =
        SqlSelectBase + " WHERE IdAttivitaDettaglio = @IdAttivitaDettaglio AND IsAttivo = 1 ORDER BY DataScadenza ASC;";

    public async Task<IReadOnlyList<ScadenzaPagamento>> GetByDettaglioAsync(Guid idAttivitaDettaglio, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd  = new CommandDefinition(SqlSelectByDettaglio, new { IdAttivitaDettaglio = idAttivitaDettaglio }, cancellationToken: ct);
        var rows = await conn.QueryAsync<ScadenzaPagamentoRow>(cmd);
        return rows.Select(ToEntity).ToList();
    }

    private const string SqlSelectById =
        SqlSelectBase + " WHERE IdScadenza = @IdScadenza;";

    public async Task<ScadenzaPagamento?> GetByIdAsync(Guid idScadenza, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(SqlSelectById, new { IdScadenza = idScadenza }, cancellationToken: ct);
        var row = await conn.QuerySingleOrDefaultAsync<ScadenzaPagamentoRow>(cmd);
        return row is null ? null : ToEntity(row);
    }

    private const string SqlInsert = """
        INSERT INTO fatt.SchedulazionePagamenti
            (IdScadenza, IdAttivitaDettaglio, DataScadenza, Importo, Nota, IsAttivo)
        VALUES
            (@IdScadenza, @IdAttivitaDettaglio, @DataScadenza, @Importo, @Nota, @IsAttivo);
        """;

    public async Task InsertAsync(ScadenzaPagamento scadenza, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(SqlInsert, ToParams(scadenza), cancellationToken: ct);
        await conn.ExecuteAsync(cmd);
    }

    private const string SqlUpdate = """
        UPDATE fatt.SchedulazionePagamenti SET
            DataScadenza = @DataScadenza,
            Importo      = @Importo,
            Nota         = @Nota
        WHERE IdScadenza = @IdScadenza;
        """;

    public async Task UpdateAsync(ScadenzaPagamento scadenza, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(SqlUpdate, ToParams(scadenza), cancellationToken: ct);
        await conn.ExecuteAsync(cmd);
    }

    private const string SqlDisattiva =
        "UPDATE fatt.SchedulazionePagamenti SET IsAttivo = 0 WHERE IdScadenza = @IdScadenza;";

    public async Task DisattivaAsync(Guid idScadenza, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(SqlDisattiva, new { IdScadenza = idScadenza }, cancellationToken: ct);
        await conn.ExecuteAsync(cmd);
    }

    private static object ToParams(ScadenzaPagamento s) => new
    {
        s.IdScadenza,
        s.IdAttivitaDettaglio,
        DataScadenza = ToSqlDate(s.DataScadenza),
        s.Importo,
        s.Nota,
        s.IsAttivo,
    };
}
