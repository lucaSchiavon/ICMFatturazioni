using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Repositories;

/// <summary>
/// Implementazione Dapper di <see cref="ITipoPagamentoRepository"/>.
/// </summary>
/// <remarks>
/// La colonna <c>FlagBanca CHAR(1)</c> passa per una DTO intermedia
/// (<see cref="TipoPagamentoRow"/>, <c>string FlagBanca</c>) e viene convertita
/// con <see cref="FlagBancaExtensions.FromDbCode"/> — stesso approccio di
/// <c>TipoAnagrafica</c>. La colonna legacy <c>TipoPagamento</c> mappa sulla
/// proprietà <see cref="TipoPagamento.Descrizione"/>.
/// </remarks>
internal sealed class TipoPagamentoRepository : ITipoPagamentoRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public TipoPagamentoRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    // DTO interna: colonne così come tornano dalla SELECT.
    private sealed class TipoPagamentoRow
    {
        public Guid IdTipoPagamento { get; init; }
        public string TipoPagamento { get; init; } = string.Empty;
        public string? SiglaPag { get; init; }
        public string FlagBanca { get; init; } = string.Empty;
        public bool IsAttivo { get; init; }
    }

    private static TipoPagamento ToEntity(TipoPagamentoRow r) => new()
    {
        IdTipoPagamento = r.IdTipoPagamento,
        Descrizione = r.TipoPagamento,
        SiglaPag = r.SiglaPag,
        FlagBanca = FlagBancaExtensions.FromDbCode(r.FlagBanca[0]),
        IsAttivo = r.IsAttivo,
    };

    private const string SqlSelect = """
        SELECT IdTipoPagamento, TipoPagamento, SiglaPag, FlagBanca, IsAttivo
        FROM fatt.TipiPagamento
        """;

    private const string SqlSelectAttivi = SqlSelect + " WHERE IsAttivo = 1 ORDER BY TipoPagamento;";

    public async Task<IReadOnlyList<TipoPagamento>> GetAttiviAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlSelectAttivi, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<TipoPagamentoRow>(cmd);
        return rows.Select(ToEntity).ToList();
    }

    private const string SqlSelectById = SqlSelect + " WHERE IdTipoPagamento = @IdTipoPagamento;";

    public async Task<TipoPagamento?> GetByIdAsync(Guid idTipoPagamento, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlSelectById, new { IdTipoPagamento = idTipoPagamento }, cancellationToken: cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<TipoPagamentoRow>(cmd);
        return row is null ? null : ToEntity(row);
    }

    private const string SqlExistsDescr = """
        SELECT CASE WHEN EXISTS (
            SELECT 1 FROM fatt.TipiPagamento
            WHERE IsAttivo = 1
              AND UPPER(TipoPagamento) = UPPER(@Descrizione)
              AND (@EscludiId IS NULL OR IdTipoPagamento <> @EscludiId)
        ) THEN 1 ELSE 0 END;
        """;

    public async Task<bool> ExistsDescrizioneAttivaAsync(string descrizione, Guid? escludiId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlExistsDescr, new { Descrizione = descrizione, EscludiId = escludiId }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(cmd);
    }

    private const string SqlExistsSigla = """
        SELECT CASE WHEN EXISTS (
            SELECT 1 FROM fatt.TipiPagamento
            WHERE IsAttivo = 1
              AND UPPER(SiglaPag) = UPPER(@SiglaPag)
              AND (@EscludiId IS NULL OR IdTipoPagamento <> @EscludiId)
        ) THEN 1 ELSE 0 END;
        """;

    public async Task<bool> ExistsSiglaAttivaAsync(string? siglaPag, Guid? escludiId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(siglaPag))
        {
            return false;
        }
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlExistsSigla, new { SiglaPag = siglaPag, EscludiId = escludiId }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(cmd);
    }

    private const string SqlInsert = """
        INSERT INTO fatt.TipiPagamento (IdTipoPagamento, TipoPagamento, SiglaPag, FlagBanca, IsAttivo)
        VALUES (@IdTipoPagamento, @TipoPagamento, @SiglaPag, @FlagBanca, @IsAttivo);
        """;

    public async Task InsertAsync(TipoPagamento tipo, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlInsert, ToParameters(tipo), cancellationToken: cancellationToken);
        await connection.ExecuteAsync(cmd);
    }

    private const string SqlUpdate = """
        UPDATE fatt.TipiPagamento SET
            TipoPagamento = @TipoPagamento,
            SiglaPag      = @SiglaPag,
            FlagBanca     = @FlagBanca,
            IsAttivo      = @IsAttivo
        WHERE IdTipoPagamento = @IdTipoPagamento;
        """;

    public async Task UpdateAsync(TipoPagamento tipo, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlUpdate, ToParameters(tipo), cancellationToken: cancellationToken);
        await connection.ExecuteAsync(cmd);
    }

    private const string SqlDisattiva = "UPDATE fatt.TipiPagamento SET IsAttivo = 0 WHERE IdTipoPagamento = @IdTipoPagamento;";

    public async Task DisattivaAsync(Guid idTipoPagamento, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlDisattiva, new { IdTipoPagamento = idTipoPagamento }, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(cmd);
    }

    public Task<bool> HasDipendenzeAsync(Guid idTipoPagamento, CancellationToken cancellationToken = default)
    {
        // I figli (fatt.CodiciPagamento) arrivano con la migration 022 (secondo
        // commit dello step): finché la tabella non esiste, un tipo non ha
        // dipendenze. Da sostituire con la query reale verso fatt.CodiciPagamento
        // quando la verticale Codici di pagamento sarà in piedi.
        return Task.FromResult(false);
    }

    private static DynamicParameters ToParameters(TipoPagamento t)
    {
        var p = new DynamicParameters();
        p.Add("IdTipoPagamento", t.IdTipoPagamento);
        p.Add("TipoPagamento",   t.Descrizione);
        p.Add("SiglaPag",        t.SiglaPag);
        // CHAR(1) sul DB: Dapper riconosce il char.
        p.Add("FlagBanca",       t.FlagBanca.ToDbCode());
        p.Add("IsAttivo",        t.IsAttivo);
        return p;
    }
}
