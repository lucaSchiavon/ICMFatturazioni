using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Models;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Repositories;

/// <summary>
/// Implementazione Dapper di <see cref="IAttivitaConsulentePagamentoRepository"/>
/// su fatt.AttivitaConsulentiPagamenti (migration 077).
/// Pagato e Residuo non sono mai memorizzati: le query li derivano sempre dalla
/// somma delle tranche attive (dispensa cap. 5: residuo = importo − Σ pagamenti).
/// </summary>
internal sealed class AttivitaConsulentePagamentoRepository : IAttivitaConsulentePagamentoRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public AttivitaConsulentePagamentoRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    // ── Read-model: righe Studio con saldo ─────────────────────────────────

    private sealed class ConsulenzaConSaldoRow
    {
        public Guid      IdAttivitaConsulente  { get; init; }
        public string    ConsulenteDescrizione { get; init; } = string.Empty;
        public string    TipoDescrizione       { get; init; } = string.Empty;
        public DateTime? Scadenza              { get; init; }
        public decimal   Importo               { get; init; }
        public decimal   Pagato                { get; init; }
    }

    // Solo righe a carico dello Studio (dispensa cap. 4-5: quelle a carico del
    // cliente non entrano nei pagamenti). Pagato = somma tranche attive.
    private const string SqlConsulenzeConSaldo = """
        SELECT ac.IdAttivitaConsulente,
               c.Consulente             AS ConsulenteDescrizione,
               t.TipoAttivitaConsulente AS TipoDescrizione,
               ac.Scadenza,
               ac.Importo,
               ISNULL(SUM(CASE WHEN p.IsAttivo = 1 THEN p.Importo END), 0) AS Pagato
        FROM fatt.AttivitaConsulenti ac
        JOIN fatt.Consulenti c
            ON c.IdConsulente = ac.IdConsulente
        JOIN fatt.TipiAttivitaConsulenti t
            ON t.IdTipoAttivitaConsulente = ac.IdTipoAttivitaConsulente
        LEFT JOIN fatt.AttivitaConsulentiPagamenti p
            ON p.IdAttivitaConsulente = ac.IdAttivitaConsulente
        WHERE ac.IdAttivita = @IdAttivita AND ac.IsAttivo = 1 AND ac.Carico = 'S'
        GROUP BY ac.IdAttivitaConsulente, c.Consulente, t.TipoAttivitaConsulente,
                 ac.Scadenza, ac.Importo
        ORDER BY c.Consulente, t.TipoAttivitaConsulente;
        """;

    public async Task<IReadOnlyList<ConsulenzaConSaldo>> GetConsulenzeConSaldoAsync(Guid idAttivita, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlConsulenzeConSaldo, new { IdAttivita = idAttivita }, cancellationToken: cancellationToken);
        var rows = await conn.QueryAsync<ConsulenzaConSaldoRow>(cmd);
        return rows.Select(r => new ConsulenzaConSaldo
        {
            IdAttivitaConsulente  = r.IdAttivitaConsulente,
            ConsulenteDescrizione = r.ConsulenteDescrizione,
            TipoDescrizione       = r.TipoDescrizione,
            Scadenza              = r.Scadenza.HasValue ? DateOnly.FromDateTime(r.Scadenza.Value) : null,
            Importo               = r.Importo,
            Pagato                = r.Pagato,
        }).ToList();
    }

    // ── Tranche ────────────────────────────────────────────────────────────

    private sealed class PagamentoRow
    {
        public Guid     IdConsulentePagamento { get; init; }
        public Guid     IdAttivitaConsulente  { get; init; }
        public DateTime DataPagamento         { get; init; }
        public decimal  Importo               { get; init; }
        public string?  Nota                  { get; init; }
        public bool     IsAttivo              { get; init; }
    }

    private static AttivitaConsulentePagamento ToEntity(PagamentoRow r) => new()
    {
        IdConsulentePagamento = r.IdConsulentePagamento,
        IdAttivitaConsulente  = r.IdAttivitaConsulente,
        DataPagamento         = DateOnly.FromDateTime(r.DataPagamento),
        Importo               = r.Importo,
        Nota                  = r.Nota,
        IsAttivo              = r.IsAttivo,
    };

    private const string SqlSelect = """
        SELECT IdConsulentePagamento, IdAttivitaConsulente, DataPagamento, Importo, Nota, IsAttivo
        FROM fatt.AttivitaConsulentiPagamenti
        """;

    private const string SqlSelectByRiga = SqlSelect + """

        WHERE IdAttivitaConsulente = @IdAttivitaConsulente AND IsAttivo = 1
        ORDER BY DataPagamento, IdConsulentePagamento;
        """;

    public async Task<IReadOnlyList<AttivitaConsulentePagamento>> GetByRigaAsync(Guid idAttivitaConsulente, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlSelectByRiga, new { IdAttivitaConsulente = idAttivitaConsulente }, cancellationToken: cancellationToken);
        var rows = await conn.QueryAsync<PagamentoRow>(cmd);
        return rows.Select(ToEntity).ToList();
    }

    private const string SqlSelectById = SqlSelect + " WHERE IdConsulentePagamento = @IdConsulentePagamento;";

    public async Task<AttivitaConsulentePagamento?> GetByIdAsync(Guid idConsulentePagamento, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlSelectById, new { IdConsulentePagamento = idConsulentePagamento }, cancellationToken: cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<PagamentoRow>(cmd);
        return row is null ? null : ToEntity(row);
    }

    // Saldo per la guardia D-C3. @EscludiPagamento permette di ricalcolare il
    // residuo in modifica ignorando la tranche che si sta cambiando.
    private const string SqlSaldoRiga = """
        SELECT ac.Importo,
               CAST(CASE WHEN ac.Carico = 'S' THEN 1 ELSE 0 END AS BIT) AS CaricoStudio,
               ISNULL((SELECT SUM(p.Importo)
                       FROM fatt.AttivitaConsulentiPagamenti p
                       WHERE p.IdAttivitaConsulente = ac.IdAttivitaConsulente
                         AND p.IsAttivo = 1
                         AND (@EscludiPagamento IS NULL OR p.IdConsulentePagamento <> @EscludiPagamento)), 0) AS Pagato
        FROM fatt.AttivitaConsulenti ac
        WHERE ac.IdAttivitaConsulente = @IdAttivitaConsulente AND ac.IsAttivo = 1;
        """;

    public async Task<SaldoRiga?> GetSaldoRigaAsync(Guid idAttivitaConsulente, Guid? escludiPagamento, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlSaldoRiga,
            new { IdAttivitaConsulente = idAttivitaConsulente, EscludiPagamento = escludiPagamento },
            cancellationToken: cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<SaldoRiga>(cmd);
    }

    private const string SqlInsert = """
        INSERT INTO fatt.AttivitaConsulentiPagamenti
            (IdConsulentePagamento, IdAttivitaConsulente, DataPagamento, Importo, Nota, IsAttivo)
        VALUES
            (@IdConsulentePagamento, @IdAttivitaConsulente, @DataPagamento, @Importo, @Nota, @IsAttivo);
        """;

    public async Task InsertAsync(AttivitaConsulentePagamento pagamento, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlInsert, ToParams(pagamento), cancellationToken: cancellationToken);
        await conn.ExecuteAsync(cmd);
    }

    // IdAttivitaConsulente volutamente fuori dal SET: una tranche non cambia riga.
    private const string SqlUpdate = """
        UPDATE fatt.AttivitaConsulentiPagamenti SET
            DataPagamento = @DataPagamento,
            Importo       = @Importo,
            Nota          = @Nota
        WHERE IdConsulentePagamento = @IdConsulentePagamento;
        """;

    public async Task UpdateAsync(AttivitaConsulentePagamento pagamento, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlUpdate, ToParams(pagamento), cancellationToken: cancellationToken);
        await conn.ExecuteAsync(cmd);
    }

    private const string SqlDisattiva = "UPDATE fatt.AttivitaConsulentiPagamenti SET IsAttivo = 0 WHERE IdConsulentePagamento = @IdConsulentePagamento;";

    public async Task DisattivaAsync(Guid idConsulentePagamento, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlDisattiva, new { IdConsulentePagamento = idConsulentePagamento }, cancellationToken: cancellationToken);
        await conn.ExecuteAsync(cmd);
    }

    private static object ToParams(AttivitaConsulentePagamento p) => new
    {
        p.IdConsulentePagamento,
        p.IdAttivitaConsulente,
        DataPagamento = p.DataPagamento.ToDateTime(TimeOnly.MinValue),
        p.Importo,
        p.Nota,
        p.IsAttivo,
    };
}
