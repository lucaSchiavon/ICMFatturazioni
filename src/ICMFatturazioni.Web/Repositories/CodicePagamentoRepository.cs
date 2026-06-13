using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Models;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Repositories;

/// <summary>
/// Implementazione Dapper di <see cref="ICodicePagamentoRepository"/>.
/// </summary>
/// <remarks>
/// La lettura "ricca" passa per una DTO (<see cref="CodicePagamentoRigaRow"/>)
/// perché la colonna <c>FlagBanca CHAR(1)</c> del tipo va convertita in enum con
/// <see cref="FlagBancaExtensions.FromDbCode"/>. La lettura per id mappa diretta
/// sull'entità (nessun enum). Le NCHAR(4) (Condizione/Modalità) si leggono come
/// stringa; in scrittura si passano <c>TRIM</c>-ate dal manager.
/// </remarks>
internal sealed class CodicePagamentoRepository : ICodicePagamentoRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public CodicePagamentoRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    // DTO della SELECT "ricca" (FlagBanca come string).
    private sealed class CodicePagamentoRigaRow
    {
        public Guid IdCodicePagamento { get; init; }
        public Guid IdTipoPagamento { get; init; }
        public string TipoDescrizione { get; init; } = string.Empty;
        public string FlagBanca { get; init; } = string.Empty;
        public string DescrPag { get; init; } = string.Empty;
        public int NumScadenze { get; init; }
        public int GGScad1 { get; init; }
        public int? GGScad2 { get; init; }
        public int? GGScad3 { get; init; }
        public int? GGpiu { get; init; }
        public bool FineMese { get; init; }
        public string? CondizionePagamento { get; init; }
        public string? CondizioneDescrizione { get; init; }
        public string? ModalitaPagamento { get; init; }
        public string? ModalitaDescrizione { get; init; }
        public bool IsAttivo { get; init; }
    }

    private static CodicePagamentoRiga ToRiga(CodicePagamentoRigaRow r) => new(
        r.IdCodicePagamento, r.IdTipoPagamento, r.TipoDescrizione,
        FlagBancaExtensions.FromDbCode(r.FlagBanca[0]),
        r.DescrPag, r.NumScadenze, r.GGScad1, r.GGScad2, r.GGScad3, r.GGpiu, r.FineMese,
        r.CondizionePagamento?.Trim(), r.CondizioneDescrizione,
        r.ModalitaPagamento?.Trim(), r.ModalitaDescrizione, r.IsAttivo);

    private const string SqlSelectRiga = """
        SELECT
            cp.IdCodicePagamento, cp.IdTipoPagamento,
            tp.TipoPagamento AS TipoDescrizione, tp.FlagBanca,
            cp.DescrPag, cp.NumScadenze, cp.GGScad1, cp.GGScad2, cp.GGScad3, cp.GGpiu, cp.FineMese,
            cp.CondizionePagamento, cond.Descrizione AS CondizioneDescrizione,
            cp.ModalitaPagamento,   moda.Descrizione AS ModalitaDescrizione,
            cp.IsAttivo
        FROM fatt.CodiciPagamento cp
        INNER JOIN fatt.TipiPagamento       tp   ON tp.IdTipoPagamento    = cp.IdTipoPagamento
        LEFT  JOIN fatt.CondizioniPagamento cond ON cond.Codice           = cp.CondizionePagamento
        LEFT  JOIN fatt.ModalitaPagamento   moda ON moda.Codice           = cp.ModalitaPagamento
        """;

    private const string SqlSelectAttivi = SqlSelectRiga + " WHERE cp.IsAttivo = 1 ORDER BY cp.DescrPag;";

    public async Task<IReadOnlyList<CodicePagamentoRiga>> GetAttiviAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlSelectAttivi, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<CodicePagamentoRigaRow>(cmd);
        return rows.Select(ToRiga).ToList();
    }

    // Lettura per id: entità grezza (per popolare il form di modifica).
    private const string SqlSelectById = """
        SELECT IdCodicePagamento, IdTipoPagamento, DescrPag, NumScadenze,
               GGScad1, GGScad2, GGScad3, GGpiu, FineMese,
               CondizionePagamento, ModalitaPagamento, IsAttivo
        FROM fatt.CodiciPagamento
        WHERE IdCodicePagamento = @IdCodicePagamento;
        """;

    public async Task<CodicePagamento?> GetByIdAsync(Guid idCodicePagamento, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlSelectById, new { IdCodicePagamento = idCodicePagamento }, cancellationToken: cancellationToken);
        var c = await connection.QuerySingleOrDefaultAsync<CodicePagamento>(cmd);
        // NCHAR(4): elimina l'eventuale padding leggendo i codici.
        if (c is null)
        {
            return null;
        }
        return new CodicePagamento
        {
            IdCodicePagamento = c.IdCodicePagamento,
            IdTipoPagamento = c.IdTipoPagamento,
            DescrPag = c.DescrPag,
            NumScadenze = c.NumScadenze,
            GGScad1 = c.GGScad1,
            GGScad2 = c.GGScad2,
            GGScad3 = c.GGScad3,
            GGpiu = c.GGpiu,
            FineMese = c.FineMese,
            CondizionePagamento = c.CondizionePagamento?.Trim(),
            ModalitaPagamento = c.ModalitaPagamento?.Trim(),
            IsAttivo = c.IsAttivo,
        };
    }

    private const string SqlExistsDescr = """
        SELECT CASE WHEN EXISTS (
            SELECT 1 FROM fatt.CodiciPagamento
            WHERE IsAttivo = 1
              AND UPPER(DescrPag) = UPPER(@Descrizione)
              AND (@EscludiId IS NULL OR IdCodicePagamento <> @EscludiId)
        ) THEN 1 ELSE 0 END;
        """;

    public async Task<bool> ExistsDescrizioneAttivaAsync(string descrizione, Guid? escludiId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlExistsDescr, new { Descrizione = descrizione, EscludiId = escludiId }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(cmd);
    }

    private const string SqlInsert = """
        INSERT INTO fatt.CodiciPagamento
            (IdCodicePagamento, IdTipoPagamento, DescrPag, NumScadenze, GGScad1, GGScad2, GGScad3, GGpiu, FineMese, CondizionePagamento, ModalitaPagamento, IsAttivo)
        VALUES
            (@IdCodicePagamento, @IdTipoPagamento, @DescrPag, @NumScadenze, @GGScad1, @GGScad2, @GGScad3, @GGpiu, @FineMese, @CondizionePagamento, @ModalitaPagamento, @IsAttivo);
        """;

    public async Task InsertAsync(CodicePagamento codice, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlInsert, ToParameters(codice), cancellationToken: cancellationToken);
        await connection.ExecuteAsync(cmd);
    }

    private const string SqlUpdate = """
        UPDATE fatt.CodiciPagamento SET
            IdTipoPagamento     = @IdTipoPagamento,
            DescrPag            = @DescrPag,
            NumScadenze         = @NumScadenze,
            GGScad1             = @GGScad1,
            GGScad2             = @GGScad2,
            GGScad3             = @GGScad3,
            GGpiu               = @GGpiu,
            FineMese            = @FineMese,
            CondizionePagamento = @CondizionePagamento,
            ModalitaPagamento   = @ModalitaPagamento,
            IsAttivo            = @IsAttivo
        WHERE IdCodicePagamento = @IdCodicePagamento;
        """;

    public async Task UpdateAsync(CodicePagamento codice, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlUpdate, ToParameters(codice), cancellationToken: cancellationToken);
        await connection.ExecuteAsync(cmd);
    }

    private const string SqlDisattiva = "UPDATE fatt.CodiciPagamento SET IsAttivo = 0 WHERE IdCodicePagamento = @IdCodicePagamento;";

    public async Task DisattivaAsync(Guid idCodicePagamento, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlDisattiva, new { IdCodicePagamento = idCodicePagamento }, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(cmd);
    }

    private const string SqlHasDipendenze = """
        SELECT CASE WHEN EXISTS (
            SELECT 1 FROM fatt.Anagrafica WHERE IdPag = @IdCodicePagamento AND IsAttivo = 1
        ) THEN 1 ELSE 0 END;
        """;

    public async Task<bool> HasDipendenzeAsync(Guid idCodicePagamento, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlHasDipendenze, new { IdCodicePagamento = idCodicePagamento }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(cmd);
    }

    private static DynamicParameters ToParameters(CodicePagamento c)
    {
        var p = new DynamicParameters();
        p.Add("IdCodicePagamento",   c.IdCodicePagamento);
        p.Add("IdTipoPagamento",     c.IdTipoPagamento);
        p.Add("DescrPag",            c.DescrPag);
        p.Add("NumScadenze",         c.NumScadenze);
        p.Add("GGScad1",             c.GGScad1);
        p.Add("GGScad2",             c.GGScad2);
        p.Add("GGScad3",             c.GGScad3);
        p.Add("GGpiu",               c.GGpiu);
        p.Add("FineMese",            c.FineMese);
        p.Add("CondizionePagamento", c.CondizionePagamento);
        p.Add("ModalitaPagamento",   c.ModalitaPagamento);
        p.Add("IsAttivo",            c.IsAttivo);
        return p;
    }
}
