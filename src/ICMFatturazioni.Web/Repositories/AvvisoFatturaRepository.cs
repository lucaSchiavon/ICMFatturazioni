using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;
using ICMFatturazioni.Web.Repositories.Interfaces;
using Microsoft.Data.SqlClient;

namespace ICMFatturazioni.Web.Repositories;

/// <summary>
/// Implementazione Dapper di <see cref="IAvvisoFatturaRepository"/>.
///
/// Le operazioni di emissione/annullamento sono <b>aggregate write</b> (Opzione B,
/// modello <c>VerbaleRepository</c> di ICMVerbali): una connessione, una transazione,
/// commit/rollback. DataAvviso è una colonna DATE: conversione DateTime ↔ DateOnly
/// come negli altri repository.
/// </summary>
internal sealed class AvvisoFatturaRepository : IAvvisoFatturaRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public AvvisoFatturaRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    // =====================================================================
    // Letture — testata
    // =====================================================================

    // DTO: DataAvviso come DateTime per compatibilità Dapper con DATE SQL.
    private sealed class AvvisoFatturaRow
    {
        public Guid     IdAvviso                 { get; init; }
        public Guid     IdAttivita               { get; init; }
        public Guid     IdAnagrafica             { get; init; }
        public DateTime DataAvviso               { get; init; }
        public string?  Oggetto                  { get; init; }
        public string?  NotaSintetica            { get; init; }
        public string?  NotaTestata              { get; init; }
        public Guid?    IdCodicePagamento        { get; init; }
        public Guid?    IdBancaAppoggio          { get; init; }
        public decimal  AliquotaIva              { get; init; }
        public decimal  AliquotaCnpaia           { get; init; }
        public decimal  AliquotaRitenuta         { get; init; }
        public bool     ApplicaRitenuta          { get; init; }
        public string?  DescrizioneSpeseInAvviso { get; init; }
        public bool     IsAttivo                 { get; init; }
        public decimal  TotaleRighe              { get; init; }
    }

    private static AvvisoFattura ToEntity(AvvisoFatturaRow r) => new()
    {
        IdAvviso                 = r.IdAvviso,
        IdAttivita               = r.IdAttivita,
        IdAnagrafica             = r.IdAnagrafica,
        DataAvviso               = DateOnly.FromDateTime(r.DataAvviso),
        Oggetto                  = r.Oggetto,
        NotaSintetica            = r.NotaSintetica,
        NotaTestata              = r.NotaTestata,
        IdCodicePagamento        = r.IdCodicePagamento,
        IdBancaAppoggio          = r.IdBancaAppoggio,
        AliquotaIva              = r.AliquotaIva,
        AliquotaCnpaia           = r.AliquotaCnpaia,
        AliquotaRitenuta         = r.AliquotaRitenuta,
        ApplicaRitenuta          = r.ApplicaRitenuta,
        DescrizioneSpeseInAvviso = r.DescrizioneSpeseInAvviso,
        IsAttivo                 = r.IsAttivo,
        TotaleRighe              = r.TotaleRighe,
    };

    // DataAvviso: DATE NOT NULL in SQL → DateTime in params.
    private static DateTime ToSqlDate(DateOnly d) => d.ToDateTime(TimeOnly.MinValue);

    // TotaleRighe: subquery di convenienza (somma importi righe dell'avviso).
    private const string SqlSelectBase = """
        SELECT
            a.IdAvviso, a.IdAttivita, a.IdAnagrafica, a.DataAvviso,
            a.Oggetto, a.NotaSintetica, a.NotaTestata,
            a.IdCodicePagamento, a.IdBancaAppoggio,
            a.AliquotaIva, a.AliquotaCnpaia, a.AliquotaRitenuta, a.ApplicaRitenuta,
            a.DescrizioneSpeseInAvviso, a.IsAttivo,
            (SELECT COALESCE(SUM(r.Importo), 0)
               FROM fatt.AvvisoFatturaRighe r
              WHERE r.IdAvviso = a.IdAvviso) AS TotaleRighe
        FROM fatt.AvvisiFattura a
        """;

    private const string SqlSelectByAttivita =
        SqlSelectBase + " WHERE a.IdAttivita = @IdAttivita AND a.IsAttivo = 1 ORDER BY a.DataAvviso DESC;";

    public async Task<IReadOnlyList<AvvisoFattura>> GetByAttivitaAsync(Guid idAttivita, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd  = new CommandDefinition(SqlSelectByAttivita, new { IdAttivita = idAttivita }, cancellationToken: ct);
        var rows = await conn.QueryAsync<AvvisoFatturaRow>(cmd);
        return rows.Select(ToEntity).ToList();
    }

    private const string SqlSelectById =
        SqlSelectBase + " WHERE a.IdAvviso = @IdAvviso;";

    public async Task<AvvisoFattura?> GetByIdAsync(Guid idAvviso, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(SqlSelectById, new { IdAvviso = idAvviso }, cancellationToken: ct);
        var row = await conn.QuerySingleOrDefaultAsync<AvvisoFatturaRow>(cmd);
        return row is null ? null : ToEntity(row);
    }

    // =====================================================================
    // Letture — righe
    // =====================================================================

    private sealed class AvvisoFatturaRigaRow
    {
        public Guid     IdRiga              { get; init; }
        public Guid     IdAvviso            { get; init; }
        public int      Ordine              { get; init; }
        public Guid?    IdAttivitaDettaglio { get; init; }
        public Guid?    IdScadenza          { get; init; }
        public string?  Tipo                { get; init; }
        public string   Descrizione         { get; init; } = string.Empty;
        public decimal? Importo             { get; init; }
        public bool     IsDescrittiva       { get; init; }
    }

    private static AvvisoFatturaRiga ToEntity(AvvisoFatturaRigaRow r) => new()
    {
        IdRiga              = r.IdRiga,
        IdAvviso            = r.IdAvviso,
        Ordine              = r.Ordine,
        IdAttivitaDettaglio = r.IdAttivitaDettaglio,
        IdScadenza          = r.IdScadenza,
        Tipo                = r.Tipo,
        Descrizione         = r.Descrizione,
        Importo             = r.Importo,
        IsDescrittiva       = r.IsDescrittiva,
    };

    private const string SqlSelectRigheByAvviso = """
        SELECT IdRiga, IdAvviso, Ordine, IdAttivitaDettaglio, IdScadenza,
               Tipo, Descrizione, Importo, IsDescrittiva
        FROM fatt.AvvisoFatturaRighe
        WHERE IdAvviso = @IdAvviso
        ORDER BY Ordine ASC;
        """;

    public async Task<IReadOnlyList<AvvisoFatturaRiga>> GetRigheByAvvisoAsync(Guid idAvviso, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd  = new CommandDefinition(SqlSelectRigheByAvviso, new { IdAvviso = idAvviso }, cancellationToken: ct);
        var rows = await conn.QueryAsync<AvvisoFatturaRigaRow>(cmd);
        return rows.Select(ToEntity).ToList();
    }

    // =====================================================================
    // Aggregate write — emissione (atomica)
    // =====================================================================

    private const string SqlInsertTestata = """
        INSERT INTO fatt.AvvisiFattura
            (IdAvviso, IdAttivita, IdAnagrafica, DataAvviso,
             Oggetto, NotaSintetica, NotaTestata,
             IdCodicePagamento, IdBancaAppoggio,
             AliquotaIva, AliquotaCnpaia, AliquotaRitenuta, ApplicaRitenuta,
             DescrizioneSpeseInAvviso, IsAttivo)
        VALUES
            (@IdAvviso, @IdAttivita, @IdAnagrafica, @DataAvviso,
             @Oggetto, @NotaSintetica, @NotaTestata,
             @IdCodicePagamento, @IdBancaAppoggio,
             @AliquotaIva, @AliquotaCnpaia, @AliquotaRitenuta, @ApplicaRitenuta,
             @DescrizioneSpeseInAvviso, @IsAttivo);
        """;

    private const string SqlInsertRiga = """
        INSERT INTO fatt.AvvisoFatturaRighe
            (IdRiga, IdAvviso, Ordine, IdAttivitaDettaglio, IdScadenza,
             Tipo, Descrizione, Importo, IsDescrittiva)
        VALUES
            (@IdRiga, @IdAvviso, @Ordine, @IdAttivitaDettaglio, @IdScadenza,
             @Tipo, @Descrizione, @Importo, @IsDescrittiva);
        """;

    // Lock rata: la scadenza consumata punta alla riga che la fattura. Il sentinel
    // "IdAvvisoRiga IS NULL" è difesa in profondità; la vera unicità è garantita
    // dall'indice UQ_AvvisoFatturaRighe_IdScadenza in fase di INSERT riga.
    private const string SqlLockScadenza = """
        UPDATE fatt.SchedulazionePagamenti
           SET IdAvvisoRiga = @IdAvvisoRiga
         WHERE IdScadenza = @IdScadenza AND IdAvvisoRiga IS NULL;
        """;

    // Link spesa → avviso ("In Avviso Del"), solo se ancora libera.
    private const string SqlLinkSpesa = """
        UPDATE fatt.SpeseAnticipate
           SET IdAvviso = @IdAvviso
         WHERE IdSpesaAnticipata = @IdSpesaAnticipata AND IdAvviso IS NULL;
        """;

    public async Task EmettiAsync(
        AvvisoFattura testata,
        IReadOnlyList<AvvisoFatturaRiga> righe,
        IReadOnlyList<Guid> idSpeseCollegate,
        CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        using var tx   = conn.BeginTransaction();
        try
        {
            // 1) Testata (prima: le righe e i link vi puntano via FK).
            await conn.ExecuteAsync(new CommandDefinition(
                SqlInsertTestata, ToParams(testata), transaction: tx, cancellationToken: ct));

            // 2) Righe (prima del lock: il marcatore scadenza punta alla riga via FK).
            //    Multi-exec Dapper: una INSERT per riga.
            if (righe.Count > 0)
                await conn.ExecuteAsync(new CommandDefinition(
                    SqlInsertRiga, righe.Select(ToRigaParams), transaction: tx, cancellationToken: ct));

            // 3) Lock delle rate consumate (solo righe reali con una scadenza).
            var lockParams = righe
                .Where(r => r.IdScadenza.HasValue)
                .Select(r => new { IdAvvisoRiga = r.IdRiga, IdScadenza = r.IdScadenza!.Value })
                .ToList();
            if (lockParams.Count > 0)
                await conn.ExecuteAsync(new CommandDefinition(
                    SqlLockScadenza, lockParams, transaction: tx, cancellationToken: ct));

            // 4) Link delle spese anticipate allegate.
            if (idSpeseCollegate.Count > 0)
            {
                var spesaParams = idSpeseCollegate
                    .Select(id => new { IdAvviso = testata.IdAvviso, IdSpesaAnticipata = id })
                    .ToList();
                await conn.ExecuteAsync(new CommandDefinition(
                    SqlLinkSpesa, spesaParams, transaction: tx, cancellationToken: ct));
            }

            tx.Commit();
        }
        // Una rata risulta già consumata da un altro avviso: l'indice univoco
        // UQ_AvvisoFatturaRighe_IdScadenza scatta all'INSERT riga. Traduce in
        // eccezione di dominio (guardia anti doppia-fatturazione sotto race).
        catch (SqlException ex) when (ex.Number is 2627 or 2601)
        {
            tx.Rollback();
            throw new AvvisoFatturaInvalidaException(
                AvvisoFatturaMotivoInvalido.ScadenzaGiaInAvviso,
                "Una delle rate selezionate è già stata inserita in un altro avviso. " +
                "Ricarica l'elenco delle scadenze da fatturare e riprova.");
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    // =====================================================================
    // Aggregate write — annullamento (atomico)
    // =====================================================================

    // Sblocca le rate: azzera il marcatore su tutte le scadenze che puntano a
    // una riga di questo avviso. Va eseguito PRIMA del DELETE righe (FK).
    private const string SqlUnlockScadenze = """
        UPDATE fatt.SchedulazionePagamenti
           SET IdAvvisoRiga = NULL
         WHERE IdAvvisoRiga IN (SELECT IdRiga FROM fatt.AvvisoFatturaRighe WHERE IdAvviso = @IdAvviso);
        """;

    private const string SqlUnlinkSpese =
        "UPDATE fatt.SpeseAnticipate SET IdAvviso = NULL WHERE IdAvviso = @IdAvviso;";

    private const string SqlDeleteRighe =
        "DELETE FROM fatt.AvvisoFatturaRighe WHERE IdAvviso = @IdAvviso;";

    private const string SqlDisattivaTestata =
        "UPDATE fatt.AvvisiFattura SET IsAttivo = 0 WHERE IdAvviso = @IdAvviso;";

    public async Task AnnullaAsync(Guid idAvviso, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        using var tx   = conn.BeginTransaction();
        try
        {
            var p = new { IdAvviso = idAvviso };

            // 1) Sblocca le rate (prima del delete righe: FK IdAvvisoRiga→IdRiga).
            await conn.ExecuteAsync(new CommandDefinition(SqlUnlockScadenze, p, transaction: tx, cancellationToken: ct));
            // 2) Scollega le spese.
            await conn.ExecuteAsync(new CommandDefinition(SqlUnlinkSpese, p, transaction: tx, cancellationToken: ct));
            // 3) Elimina le righe (libera l'indice univoco: rate ri-fatturabili).
            await conn.ExecuteAsync(new CommandDefinition(SqlDeleteRighe, p, transaction: tx, cancellationToken: ct));
            // 4) Soft-delete della testata.
            await conn.ExecuteAsync(new CommandDefinition(SqlDisattivaTestata, p, transaction: tx, cancellationToken: ct));

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    // =====================================================================
    // Update testata (single-table, snapshot fiscali immutabili)
    // =====================================================================

    private const string SqlUpdate = """
        UPDATE fatt.AvvisiFattura SET
            DataAvviso               = @DataAvviso,
            Oggetto                  = @Oggetto,
            NotaSintetica            = @NotaSintetica,
            NotaTestata              = @NotaTestata,
            IdCodicePagamento        = @IdCodicePagamento,
            IdBancaAppoggio          = @IdBancaAppoggio,
            DescrizioneSpeseInAvviso = @DescrizioneSpeseInAvviso
        WHERE IdAvviso = @IdAvviso;
        """;

    public async Task UpdateAsync(AvvisoFattura avviso, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(SqlUpdate, ToParams(avviso), cancellationToken: ct);
        await conn.ExecuteAsync(cmd);
    }

    // =====================================================================
    // Mapping parametri
    // =====================================================================

    private static object ToParams(AvvisoFattura a) => new
    {
        a.IdAvviso,
        a.IdAttivita,
        a.IdAnagrafica,
        DataAvviso = ToSqlDate(a.DataAvviso),
        a.Oggetto,
        a.NotaSintetica,
        a.NotaTestata,
        a.IdCodicePagamento,
        a.IdBancaAppoggio,
        a.AliquotaIva,
        a.AliquotaCnpaia,
        a.AliquotaRitenuta,
        a.ApplicaRitenuta,
        a.DescrizioneSpeseInAvviso,
        a.IsAttivo,
    };

    private static object ToRigaParams(AvvisoFatturaRiga r) => new
    {
        r.IdRiga,
        r.IdAvviso,
        r.Ordine,
        r.IdAttivitaDettaglio,
        r.IdScadenza,
        r.Tipo,
        r.Descrizione,
        r.Importo,
        r.IsDescrittiva,
    };
}
