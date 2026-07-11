using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Repositories;

/// <summary>
/// Implementazione Dapper di <see cref="IAttivitaConsulenteRepository"/> su
/// fatt.AttivitaConsulenti (migration 077).
///   • Carico: CHAR(1) DB ↔ enum <see cref="CaricoConsulenza"/> via estensioni.
///   • Scadenza: DTO con <c>DateTime?</c> → <c>DateOnly?</c> (stesso pattern di
///     AttivitaDettaglioRepository).
///   • DisattivaAsync porta il sentinel D-C2: nessuna disattivazione se esistono
///     tranche di pagamento attive.
/// </summary>
internal sealed class AttivitaConsulenteRepository : IAttivitaConsulenteRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public AttivitaConsulenteRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    private sealed class AttivitaConsulenteRow
    {
        public Guid      IdAttivitaConsulente     { get; init; }
        public Guid      IdAttivita               { get; init; }
        public Guid      IdConsulente             { get; init; }
        public Guid      IdTipoAttivitaConsulente { get; init; }
        public string    Carico                   { get; init; } = "S";
        public decimal   Importo                  { get; init; }
        public DateTime? Scadenza                 { get; init; }
        public string?   Nota                     { get; init; }
        public bool      IsAttivo                 { get; init; }
        public string?   ConsulenteDescrizione             { get; init; }
        public string?   TipoAttivitaConsulenteDescrizione { get; init; }
    }

    private static AttivitaConsulente ToEntity(AttivitaConsulenteRow r) => new()
    {
        IdAttivitaConsulente     = r.IdAttivitaConsulente,
        IdAttivita               = r.IdAttivita,
        IdConsulente             = r.IdConsulente,
        IdTipoAttivitaConsulente = r.IdTipoAttivitaConsulente,
        Carico                   = CaricoConsulenzaExtensions.CaricoConsulenzaFromDbCode(r.Carico[0]),
        Importo                  = r.Importo,
        Scadenza                 = r.Scadenza.HasValue ? DateOnly.FromDateTime(r.Scadenza.Value) : null,
        Nota                     = r.Nota,
        IsAttivo                 = r.IsAttivo,
        ConsulenteDescrizione             = r.ConsulenteDescrizione,
        TipoAttivitaConsulenteDescrizione = r.TipoAttivitaConsulenteDescrizione,
    };

    private const string SqlSelect = """
        SELECT ac.IdAttivitaConsulente, ac.IdAttivita, ac.IdConsulente,
               ac.IdTipoAttivitaConsulente, ac.Carico, ac.Importo, ac.Scadenza,
               ac.Nota, ac.IsAttivo,
               c.Consulente             AS ConsulenteDescrizione,
               t.TipoAttivitaConsulente AS TipoAttivitaConsulenteDescrizione
        FROM fatt.AttivitaConsulenti ac
        JOIN fatt.Consulenti c
            ON c.IdConsulente = ac.IdConsulente
        JOIN fatt.TipiAttivitaConsulenti t
            ON t.IdTipoAttivitaConsulente = ac.IdTipoAttivitaConsulente
        """;

    private const string SqlSelectByAttivita = SqlSelect + """

        WHERE ac.IdAttivita = @IdAttivita AND ac.IsAttivo = 1
        ORDER BY c.Consulente, t.TipoAttivitaConsulente;
        """;

    public async Task<IReadOnlyList<AttivitaConsulente>> GetByAttivitaAsync(Guid idAttivita, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlSelectByAttivita, new { IdAttivita = idAttivita }, cancellationToken: cancellationToken);
        var rows = await conn.QueryAsync<AttivitaConsulenteRow>(cmd);
        return rows.Select(ToEntity).ToList();
    }

    private const string SqlSelectById = SqlSelect + " WHERE ac.IdAttivitaConsulente = @IdAttivitaConsulente;";

    public async Task<AttivitaConsulente?> GetByIdAsync(Guid idAttivitaConsulente, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlSelectById, new { IdAttivitaConsulente = idAttivitaConsulente }, cancellationToken: cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<AttivitaConsulenteRow>(cmd);
        return row is null ? null : ToEntity(row);
    }

    private const string SqlInsert = """
        INSERT INTO fatt.AttivitaConsulenti
            (IdAttivitaConsulente, IdAttivita, IdConsulente, IdTipoAttivitaConsulente,
             Carico, Importo, Scadenza, Nota, IsAttivo)
        VALUES
            (@IdAttivitaConsulente, @IdAttivita, @IdConsulente, @IdTipoAttivitaConsulente,
             @Carico, @Importo, @Scadenza, @Nota, @IsAttivo);
        """;

    public async Task InsertAsync(AttivitaConsulente riga, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlInsert, ToParams(riga), cancellationToken: cancellationToken);
        await conn.ExecuteAsync(cmd);
    }

    // IdAttivita volutamente fuori dal SET: una riga consulenza non cambia attività.
    private const string SqlUpdate = """
        UPDATE fatt.AttivitaConsulenti SET
            IdConsulente             = @IdConsulente,
            IdTipoAttivitaConsulente = @IdTipoAttivitaConsulente,
            Carico                   = @Carico,
            Importo                  = @Importo,
            Scadenza                 = @Scadenza,
            Nota                     = @Nota
        WHERE IdAttivitaConsulente = @IdAttivitaConsulente;
        """;

    public async Task UpdateAsync(AttivitaConsulente riga, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlUpdate, ToParams(riga), cancellationToken: cancellationToken);
        await conn.ExecuteAsync(cmd);
    }

    // Sentinel D-C2: la riga non si disattiva se ha tranche di pagamento attive,
    // anche sotto race condition o se il pre-check del manager fosse aggirato.
    private const string SqlDisattiva = """
        UPDATE fatt.AttivitaConsulenti SET IsAttivo = 0
        WHERE IdAttivitaConsulente = @IdAttivitaConsulente
          AND NOT EXISTS (
              SELECT 1 FROM fatt.AttivitaConsulentiPagamenti p
              WHERE p.IdAttivitaConsulente = @IdAttivitaConsulente AND p.IsAttivo = 1);
        """;

    public async Task DisattivaAsync(Guid idAttivitaConsulente, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlDisattiva, new { IdAttivitaConsulente = idAttivitaConsulente }, cancellationToken: cancellationToken);
        await conn.ExecuteAsync(cmd);
    }

    private const string SqlHasPagamenti = """
        SELECT CASE WHEN EXISTS (
            SELECT 1 FROM fatt.AttivitaConsulentiPagamenti p
            WHERE p.IdAttivitaConsulente = @IdAttivitaConsulente AND p.IsAttivo = 1
        ) THEN 1 ELSE 0 END;
        """;

    public async Task<bool> HasPagamentiAsync(Guid idAttivitaConsulente, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlHasPagamenti, new { IdAttivitaConsulente = idAttivitaConsulente }, cancellationToken: cancellationToken);
        return await conn.ExecuteScalarAsync<bool>(cmd);
    }

    private const string SqlPagato = """
        SELECT ISNULL(SUM(p.Importo), 0)
        FROM fatt.AttivitaConsulentiPagamenti p
        WHERE p.IdAttivitaConsulente = @IdAttivitaConsulente AND p.IsAttivo = 1;
        """;

    public async Task<decimal> GetPagatoAsync(Guid idAttivitaConsulente, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlPagato, new { IdAttivitaConsulente = idAttivitaConsulente }, cancellationToken: cancellationToken);
        return await conn.ExecuteScalarAsync<decimal>(cmd);
    }

    private static object ToParams(AttivitaConsulente r) => new
    {
        r.IdAttivitaConsulente,
        r.IdAttivita,
        r.IdConsulente,
        r.IdTipoAttivitaConsulente,
        Carico   = r.Carico.ToDbCode().ToString(),
        r.Importo,
        Scadenza = r.Scadenza.HasValue ? r.Scadenza.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
        r.Nota,
        r.IsAttivo,
    };
}
