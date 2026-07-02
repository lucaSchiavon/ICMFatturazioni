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
        public Guid?    IdAvviso          { get; init; }
        public bool     IsAttivo          { get; init; }
        // Nav dell'avviso che ha associato la spesa (solo nella lettura per-attività).
        public DateTime? AvvisoDataAssociazione    { get; init; }
        public string?   AvvisoOggettoAssociazione { get; init; }
    }

    private static SpesaAnticipata ToEntity(SpesaAnticipataRow r) => new()
    {
        IdSpesaAnticipata = r.IdSpesaAnticipata,
        IdAttivita        = r.IdAttivita,
        Data              = DateOnly.FromDateTime(r.Data),
        Descrizione       = r.Descrizione,
        Importo           = r.Importo,
        IdAvviso          = r.IdAvviso,
        IsAttivo          = r.IsAttivo,
        AvvisoDataAssociazione    = r.AvvisoDataAssociazione is { } d ? DateOnly.FromDateTime(d) : null,
        AvvisoOggettoAssociazione = r.AvvisoOggettoAssociazione,
    };

    // Data: DATE NOT NULL in SQL → DateTime in params.
    private static DateTime ToSqlDate(DateOnly d) => d.ToDateTime(TimeOnly.MinValue);

    private const string SqlSelectBase = """
        SELECT IdSpesaAnticipata, IdAttivita, Data, Descrizione, Importo, IdAvviso, IsAttivo
        FROM fatt.SpeseAnticipate
        """;

    // Lettura per-attività arricchita con l'avviso che ha associato ciascuna spesa
    // (data + oggetto), per mostrare in Gestione Spese il lock "In avviso del…".
    private const string SqlSelectByAttivita = """
        SELECT s.IdSpesaAnticipata, s.IdAttivita, s.Data, s.Descrizione, s.Importo,
               s.IdAvviso, s.IsAttivo,
               a.DataAvviso AS AvvisoDataAssociazione,
               a.Oggetto    AS AvvisoOggettoAssociazione
        FROM fatt.SpeseAnticipate s
        LEFT JOIN fatt.AvvisiFattura a ON a.IdAvviso = s.IdAvviso AND a.IsAttivo = 1
        WHERE s.IdAttivita = @IdAttivita AND s.IsAttivo = 1
        ORDER BY s.Data ASC;
        """;

    public async Task<IReadOnlyList<SpesaAnticipata>> GetByAttivitaAsync(Guid idAttivita, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd  = new CommandDefinition(SqlSelectByAttivita, new { IdAttivita = idAttivita }, cancellationToken: ct);
        var rows = await conn.QueryAsync<SpesaAnticipataRow>(cmd);
        return rows.Select(ToEntity).ToList();
    }

    // Spese ancora fatturabili: attive e non ancora collegate ad alcun avviso.
    private const string SqlSelectFatturabiliByAttivita =
        SqlSelectBase + " WHERE IdAttivita = @IdAttivita AND IsAttivo = 1 AND IdAvviso IS NULL ORDER BY Data ASC;";

    public async Task<IReadOnlyList<SpesaAnticipata>> GetFatturabiliByAttivitaAsync(Guid idAttivita, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd  = new CommandDefinition(SqlSelectFatturabiliByAttivita, new { IdAttivita = idAttivita }, cancellationToken: ct);
        var rows = await conn.QueryAsync<SpesaAnticipataRow>(cmd);
        return rows.Select(ToEntity).ToList();
    }

    // Spese riaddebitate in uno specifico avviso (attive), per la cascata art.15.
    private const string SqlSelectByAvviso =
        SqlSelectBase + " WHERE IdAvviso = @IdAvviso AND IsAttivo = 1 ORDER BY Data ASC;";

    public async Task<IReadOnlyList<SpesaAnticipata>> GetByAvvisoAsync(Guid idAvviso, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd  = new CommandDefinition(SqlSelectByAvviso, new { IdAvviso = idAvviso }, cancellationToken: ct);
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

    // Sentinel di correttezza (doppia difesa, CLAUDE.md): una spesa associata a un
    // avviso (IdAvviso valorizzato) è congelata → l'UPDATE non tocca righe.
    private const string SqlUpdate = """
        UPDATE fatt.SpeseAnticipate SET
            Data        = @Data,
            Descrizione = @Descrizione,
            Importo     = @Importo
        WHERE IdSpesaAnticipata = @IdSpesaAnticipata AND IdAvviso IS NULL;
        """;

    public async Task UpdateAsync(SpesaAnticipata spesa, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(SqlUpdate, ToParams(spesa), cancellationToken: ct);
        await conn.ExecuteAsync(cmd);
    }

    // Sentinel: non si soft-elimina una spesa associata a un avviso (congelata).
    private const string SqlDisattiva =
        "UPDATE fatt.SpeseAnticipate SET IsAttivo = 0 WHERE IdSpesaAnticipata = @IdSpesaAnticipata AND IdAvviso IS NULL;";

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
