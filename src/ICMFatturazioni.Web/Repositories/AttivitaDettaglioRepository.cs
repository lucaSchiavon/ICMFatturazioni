using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Repositories;

/// <summary>
/// Implementazione Dapper di <see cref="IAttivitaDettaglioRepository"/>.
/// Stessa tecnica di <c>AttivitaRepository</c> per le colonne DATE:
///   • Lettura: DTO con <c>DateTime?</c> → <c>DateOnly?</c> tramite <c>FromSqlDate</c>.
///   • Scrittura: <c>DateOnly?</c> → <c>DateTime?</c> tramite <c>ToSqlDate</c>.
/// Lo swap degli ordini usa un ordine temporaneo -999 per rispettare
/// il UNIQUE (IdAttivita, Ordine) senza disabilitarlo.
/// </summary>
internal sealed class AttivitaDettaglioRepository : IAttivitaDettaglioRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public AttivitaDettaglioRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    // DTO: colonne DATE lette come DateTime? per compatibilità Dapper.
    private sealed class AttivitaDettaglioRow
    {
        public Guid     IdAttivitaDettaglio     { get; init; }
        public Guid     IdAttivita              { get; init; }
        public Guid     IdTipoDettaglioAttivita { get; init; }
        public int      Ordine                  { get; init; }
        public string   DescrizioneDettaglio    { get; init; } = string.Empty;
        public decimal  Importo                 { get; init; }
        public string?  NotaDettaglio           { get; init; }
        public DateTime? TerminePrevisto        { get; init; }
        public bool     HasFattura              { get; init; }
        public bool     IsAttivo                { get; init; }
        // Join di convenienza
        public string?  TipoDettaglioDescrizione { get; init; }
        // Subquery aggregate
        public int      NumeroScadenze          { get; init; }
        public decimal  TotaleScadenzato        { get; init; }
    }

    private static DateOnly? FromSqlDate(DateTime? dt)
        => dt.HasValue ? DateOnly.FromDateTime(dt.Value) : null;

    private static DateTime? ToSqlDate(DateOnly? d)
        => d.HasValue ? d.Value.ToDateTime(TimeOnly.MinValue) : null;

    private static AttivitaDettaglio ToEntity(AttivitaDettaglioRow r) => new()
    {
        IdAttivitaDettaglio     = r.IdAttivitaDettaglio,
        IdAttivita              = r.IdAttivita,
        IdTipoDettaglioAttivita = r.IdTipoDettaglioAttivita,
        Ordine                  = r.Ordine,
        DescrizioneDettaglio    = r.DescrizioneDettaglio,
        Importo                 = r.Importo,
        NotaDettaglio           = r.NotaDettaglio,
        TerminePrevisto         = FromSqlDate(r.TerminePrevisto),
        HasFattura              = r.HasFattura,
        IsAttivo                = r.IsAttivo,
        TipoDettaglioDescrizione = r.TipoDettaglioDescrizione,
        NumeroScadenze          = r.NumeroScadenze,
        TotaleScadenzato        = r.TotaleScadenzato,
    };

    private const string SqlSelectBase = """
        SELECT d.IdAttivitaDettaglio, d.IdAttivita, d.IdTipoDettaglioAttivita, d.Ordine,
               d.DescrizioneDettaglio, d.Importo, d.NotaDettaglio, d.TerminePrevisto,
               d.HasFattura, d.IsAttivo,
               td.TipoDettaglioAttivita AS TipoDettaglioDescrizione,
               (SELECT COUNT(*)
                FROM fatt.SchedulazionePagamenti sp
                WHERE sp.IdAttivitaDettaglio = d.IdAttivitaDettaglio AND sp.IsAttivo = 1
               ) AS NumeroScadenze,
               (SELECT ISNULL(SUM(sp.Importo), 0)
                FROM fatt.SchedulazionePagamenti sp
                WHERE sp.IdAttivitaDettaglio = d.IdAttivitaDettaglio AND sp.IsAttivo = 1
               ) AS TotaleScadenzato
        FROM fatt.AttivitaDettaglio d
        LEFT JOIN fatt.TipiDettaglioAttivita td
               ON td.IdTipoDettaglioAttivita = d.IdTipoDettaglioAttivita
        """;

    private const string SqlSelectByAttivita =
        SqlSelectBase + " WHERE d.IdAttivita = @IdAttivita AND d.IsAttivo = 1 ORDER BY d.Ordine ASC;";

    public async Task<IReadOnlyList<AttivitaDettaglio>> GetByAttivitaAsync(Guid idAttivita, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd  = new CommandDefinition(SqlSelectByAttivita, new { IdAttivita = idAttivita }, cancellationToken: ct);
        var rows = await conn.QueryAsync<AttivitaDettaglioRow>(cmd);
        return rows.Select(ToEntity).ToList();
    }

    private const string SqlSelectById =
        SqlSelectBase + " WHERE d.IdAttivitaDettaglio = @IdAttivitaDettaglio;";

    public async Task<AttivitaDettaglio?> GetByIdAsync(Guid idAttivitaDettaglio, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(SqlSelectById, new { IdAttivitaDettaglio = idAttivitaDettaglio }, cancellationToken: ct);
        var row = await conn.QuerySingleOrDefaultAsync<AttivitaDettaglioRow>(cmd);
        return row is null ? null : ToEntity(row);
    }

    // Il MAX va calcolato su TUTTE le righe dell'attività (attive E soft-deletate):
    // il vincolo UNIQUE (IdAttivita, Ordine) non è filtrato, quindi anche una riga
    // con IsAttivo = 0 continua a "occupare" il suo Ordine. Filtrare su IsAttivo = 1
    // farebbe riusare l'Ordine di una riga cancellata → violazione del vincolo.
    // I buchi di numerazione tra le righe attive sono innocui (lista ORDER BY Ordine).
    private const string SqlMaxOrdine = """
        SELECT ISNULL(MAX(Ordine), 0)
        FROM fatt.AttivitaDettaglio
        WHERE IdAttivita = @IdAttivita;
        """;

    public async Task<int> GetMaxOrdineAsync(Guid idAttivita, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(SqlMaxOrdine, new { IdAttivita = idAttivita }, cancellationToken: ct);
        return await conn.ExecuteScalarAsync<int>(cmd);
    }

    private const string SqlInsert = """
        INSERT INTO fatt.AttivitaDettaglio
            (IdAttivitaDettaglio, IdAttivita, IdTipoDettaglioAttivita, Ordine,
             DescrizioneDettaglio, Importo, NotaDettaglio, TerminePrevisto,
             HasFattura, IsAttivo)
        VALUES
            (@IdAttivitaDettaglio, @IdAttivita, @IdTipoDettaglioAttivita, @Ordine,
             @DescrizioneDettaglio, @Importo, @NotaDettaglio, @TerminePrevisto,
             @HasFattura, @IsAttivo);
        """;

    public async Task InsertAsync(AttivitaDettaglio dettaglio, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(SqlInsert, ToParams(dettaglio), cancellationToken: ct);
        await conn.ExecuteAsync(cmd);
    }

    private const string SqlUpdate = """
        UPDATE fatt.AttivitaDettaglio SET
            IdTipoDettaglioAttivita = @IdTipoDettaglioAttivita,
            Ordine                  = @Ordine,
            DescrizioneDettaglio    = @DescrizioneDettaglio,
            Importo                 = @Importo,
            NotaDettaglio           = @NotaDettaglio,
            TerminePrevisto         = @TerminePrevisto,
            IsAttivo                = @IsAttivo
        WHERE IdAttivitaDettaglio = @IdAttivitaDettaglio;
        """;

    public async Task UpdateAsync(AttivitaDettaglio dettaglio, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(SqlUpdate, ToParams(dettaglio), cancellationToken: ct);
        await conn.ExecuteAsync(cmd);
    }

    private const string SqlDisattiva =
        "UPDATE fatt.AttivitaDettaglio SET IsAttivo = 0 WHERE IdAttivitaDettaglio = @IdAttivitaDettaglio;";

    public async Task DisattivaAsync(Guid idAttivitaDettaglio, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(SqlDisattiva, new { IdAttivitaDettaglio = idAttivitaDettaglio }, cancellationToken: ct);
        await conn.ExecuteAsync(cmd);
    }

    // Swap in tre passi con ordine temporaneo -999.
    // Step 1: porta A a -999 (non può già esistere, gli ordini reali partono da 1).
    // Step 2: porta B all'ordine originale di A.
    // Step 3: porta A all'ordine originale di B.
    private const string SqlSwapOrdineStep1 =
        "UPDATE fatt.AttivitaDettaglio SET Ordine = -999 WHERE IdAttivitaDettaglio = @Id;";
    private const string SqlSwapOrdineStep2 =
        "UPDATE fatt.AttivitaDettaglio SET Ordine = @Ordine WHERE IdAttivitaDettaglio = @Id;";

    public async Task ScambiaOrdineAsync(Guid idA, Guid idB, int ordineA, int ordineB, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        using var tx   = conn.BeginTransaction();
        try
        {
            await conn.ExecuteAsync(new CommandDefinition(SqlSwapOrdineStep1,
                new { Id = idA }, transaction: tx, cancellationToken: ct));
            await conn.ExecuteAsync(new CommandDefinition(SqlSwapOrdineStep2,
                new { Id = idB, Ordine = ordineA }, transaction: tx, cancellationToken: ct));
            await conn.ExecuteAsync(new CommandDefinition(SqlSwapOrdineStep2,
                new { Id = idA, Ordine = ordineB }, transaction: tx, cancellationToken: ct));
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private static object ToParams(AttivitaDettaglio d) => new
    {
        d.IdAttivitaDettaglio,
        d.IdAttivita,
        d.IdTipoDettaglioAttivita,
        d.Ordine,
        d.DescrizioneDettaglio,
        d.Importo,
        d.NotaDettaglio,
        TerminePrevisto = ToSqlDate(d.TerminePrevisto),
        d.HasFattura,
        d.IsAttivo,
    };
}
