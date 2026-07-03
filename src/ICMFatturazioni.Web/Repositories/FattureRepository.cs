using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;
using ICMFatturazioni.Web.Repositories.Interfaces;
using Microsoft.Data.SqlClient;

namespace ICMFatturazioni.Web.Repositories;

/// <summary>
/// Implementazione Dapper di <see cref="IFattureRepository"/>. Tabella single-table:
/// nessuna transazione multi-tabella (a differenza dell'avviso). DataFattura è una
/// colonna DATE: conversione DateTime ↔ DateOnly come negli altri repository.
/// </summary>
internal sealed class FattureRepository : IFattureRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public FattureRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    // DataFattura: DATE in SQL → DateTime in params/Dapper.
    private static DateTime ToSqlDate(DateOnly d) => d.ToDateTime(TimeOnly.MinValue);

    // DTO: DataFattura come DateTime per compatibilità Dapper con DATE SQL.
    private sealed class FatturaRow
    {
        public Guid     IdFattura     { get; init; }
        public Guid     IdAvviso      { get; init; }
        public int      NumeroFattura { get; init; }
        public int      Anno          { get; init; }
        public DateTime DataFattura   { get; init; }
        public bool     CreatoXML     { get; init; }
        public int      EsitoXML      { get; init; }
        public bool     IsAttivo      { get; init; }
    }

    private static Fattura ToEntity(FatturaRow r) => new()
    {
        IdFattura     = r.IdFattura,
        IdAvviso      = r.IdAvviso,
        NumeroFattura = r.NumeroFattura,
        Anno          = r.Anno,
        DataFattura   = DateOnly.FromDateTime(r.DataFattura),
        CreatoXML     = r.CreatoXML,
        EsitoXML      = r.EsitoXML,
        IsAttivo      = r.IsAttivo,
    };

    private const string SqlSelectBase = """
        SELECT IdFattura, IdAvviso, NumeroFattura, Anno, DataFattura,
               CreatoXML, EsitoXML, IsAttivo
        FROM fatt.Fatture
        """;

    public async Task<Fattura?> GetByIdAsync(Guid idFattura, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(SqlSelectBase + " WHERE IdFattura = @IdFattura;",
            new { IdFattura = idFattura }, cancellationToken: ct);
        var row = await conn.QuerySingleOrDefaultAsync<FatturaRow>(cmd);
        return row is null ? null : ToEntity(row);
    }

    public async Task<Fattura?> GetAttivaByAvvisoAsync(Guid idAvviso, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(
            SqlSelectBase + " WHERE IdAvviso = @IdAvviso AND IsAttivo = 1;",
            new { IdAvviso = idAvviso }, cancellationToken: ct);
        var row = await conn.QuerySingleOrDefaultAsync<FatturaRow>(cmd);
        return row is null ? null : ToEntity(row);
    }

    private const string SqlMaxNumeroAnno = """
        SELECT COALESCE(MAX(NumeroFattura), 0)
        FROM fatt.Fatture
        WHERE Anno = @Anno AND IsAttivo = 1;
        """;

    public async Task<int> GetMaxNumeroAnnoAsync(int anno, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(SqlMaxNumeroAnno, new { Anno = anno }, cancellationToken: ct);
        return await conn.ExecuteScalarAsync<int>(cmd);
    }

    private const string SqlInsert = """
        INSERT INTO fatt.Fatture
            (IdFattura, IdAvviso, NumeroFattura, Anno, DataFattura, CreatoXML, EsitoXML, IsAttivo)
        VALUES
            (@IdFattura, @IdAvviso, @NumeroFattura, @Anno, @DataFattura, @CreatoXML, @EsitoXML, @IsAttivo);
        """;

    public async Task CreateAsync(Fattura fattura, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(SqlInsert, new
        {
            fattura.IdFattura,
            fattura.IdAvviso,
            fattura.NumeroFattura,
            fattura.Anno,
            DataFattura = ToSqlDate(fattura.DataFattura),
            fattura.CreatoXML,
            fattura.EsitoXML,
            fattura.IsAttivo,
        }, cancellationToken: ct);

        try
        {
            await conn.ExecuteAsync(cmd);
        }
        // Violazione di un indice univoco filtrato: o l'avviso ha già una fattura
        // attiva (UQ_Fatture_IdAvviso_Attiva), o il numero è già usato nell'anno
        // (UQ_Fatture_Anno_Numero_Attiva). Distinguo dal nome del vincolo nel messaggio.
        catch (SqlException ex) when (ex.Number is 2627 or 2601)
        {
            if (ex.Message.Contains("UQ_Fatture_IdAvviso_Attiva", StringComparison.OrdinalIgnoreCase))
                throw new FatturaInvalidaException(
                    FatturaMotivoInvalido.AvvisoGiaFatturato,
                    "Questo avviso è già stato fatturato. Ricarica l'elenco degli avvisi non fatturati.");

            throw new FatturaInvalidaException(
                FatturaMotivoInvalido.NumeroDuplicato,
                "Il numero di fattura indicato è già usato per l'anno corrente. " +
                "Ricarica la maschera per riproporre il numero successivo.");
        }
    }

    private const string SqlAnnulla =
        "UPDATE fatt.Fatture SET IsAttivo = 0 WHERE IdFattura = @IdFattura;";

    public async Task AnnullaAsync(Guid idFattura, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(SqlAnnulla, new { IdFattura = idFattura }, cancellationToken: ct);
        await conn.ExecuteAsync(cmd);
    }
}
