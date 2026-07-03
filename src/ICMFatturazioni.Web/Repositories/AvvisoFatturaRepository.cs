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
        public decimal  TotaleSpese              { get; init; }
        public Guid?    IdFattura                { get; init; }
        public int?     NumeroFattura            { get; init; }
        public int?     AnnoFattura              { get; init; }
        public DateTime? DataFattura             { get; init; }
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
        TotaleSpese              = r.TotaleSpese,
        IdFattura                = r.IdFattura,
        NumeroFattura            = r.NumeroFattura,
        AnnoFattura              = r.AnnoFattura,
        DataFattura              = r.DataFattura is { } d ? DateOnly.FromDateTime(d) : null,
    };

    // DataAvviso: DATE NOT NULL in SQL → DateTime in params.
    private static DateTime ToSqlDate(DateOnly d) => d.ToDateTime(TimeOnly.MinValue);

    // TotaleRighe / TotaleSpese: subquery di convenienza (somma importi righe e somma
    // spese anticipate collegate). Servono a riconoscere gli avvisi di "sole spese".
    private const string SqlSelectBase = """
        SELECT
            a.IdAvviso, a.IdAttivita, a.IdAnagrafica, a.DataAvviso,
            a.Oggetto, a.NotaSintetica, a.NotaTestata,
            a.IdCodicePagamento, a.IdBancaAppoggio,
            a.AliquotaIva, a.AliquotaCnpaia, a.AliquotaRitenuta, a.ApplicaRitenuta,
            a.DescrizioneSpeseInAvviso, a.IsAttivo,
            (SELECT COALESCE(SUM(r.Importo), 0)
               FROM fatt.AvvisoFatturaRighe r
              WHERE r.IdAvviso = a.IdAvviso) AS TotaleRighe,
            (SELECT COALESCE(SUM(s.Importo), 0)
               FROM fatt.SpeseAnticipate s
              WHERE s.IdAvviso = a.IdAvviso) AS TotaleSpese,
            -- Stato di fatturazione: LEFT JOIN sulla fattura ATTIVA collegata.
            f.IdFattura, f.NumeroFattura, f.Anno AS AnnoFattura, f.DataFattura
        FROM fatt.AvvisiFattura a
        LEFT JOIN fatt.Fatture f ON f.IdAvviso = a.IdAvviso AND f.IsAttivo = 1
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

    // Coppie (cliente, attività) con almeno un avviso attivo non ancora fatturato.
    private const string SqlAttivitaConAvvisiNonFatturati = """
        SELECT DISTINCT a.IdAnagrafica, a.IdAttivita
        FROM fatt.AvvisiFattura a
        WHERE a.IsAttivo = 1
          AND NOT EXISTS (
              SELECT 1 FROM fatt.Fatture f
              WHERE f.IdAvviso = a.IdAvviso AND f.IsAttivo = 1);
        """;

    public async Task<IReadOnlyList<Models.AttivitaFatturabile>> GetAttivitaConAvvisiNonFatturatiAsync(CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd  = new CommandDefinition(SqlAttivitaConAvvisiNonFatturati, cancellationToken: ct);
        var rows = await conn.QueryAsync<Models.AttivitaFatturabile>(cmd);
        return rows.ToList();
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
    // Grandezze "Dettagli Attività" per l'avviso selezionato
    // =====================================================================

    private sealed class DettaglioGrandezzeRow
    {
        public Guid      IdAttivitaDettaglio { get; init; }
        public string?   Tipo                { get; init; }
        public string    Descrizione         { get; init; } = string.Empty;
        public DateTime? DataScadenza        { get; init; }
        public decimal   ImportoDettaglio    { get; init; }
        public decimal   AltriAvvisi         { get; init; }
        public decimal   AvvisoAttuale       { get; init; }
    }

    private const string SqlDettagliGrandezze = """
        SELECT
            d.IdAttivitaDettaglio,
            td.TipoDettaglioAttivita AS Tipo,
            d.DescrizioneDettaglio   AS Descrizione,
            d.Importo                AS ImportoDettaglio,
            (SELECT MIN(sp.DataScadenza)
               FROM fatt.SchedulazionePagamenti sp
               JOIN fatt.AvvisoFatturaRighe rr ON rr.IdRiga = sp.IdAvvisoRiga
              WHERE rr.IdAvviso = @IdAvviso
                AND sp.IdAttivitaDettaglio = d.IdAttivitaDettaglio) AS DataScadenza,
            (SELECT COALESCE(SUM(r2.Importo), 0)
               FROM fatt.AvvisoFatturaRighe r2
               JOIN fatt.AvvisiFattura a2 ON a2.IdAvviso = r2.IdAvviso AND a2.IsAttivo = 1
              WHERE r2.IdAttivitaDettaglio = d.IdAttivitaDettaglio
                AND r2.IdAvviso <> @IdAvviso) AS AltriAvvisi,
            (SELECT COALESCE(SUM(r3.Importo), 0)
               FROM fatt.AvvisoFatturaRighe r3
              WHERE r3.IdAttivitaDettaglio = d.IdAttivitaDettaglio
                AND r3.IdAvviso = @IdAvviso) AS AvvisoAttuale
        FROM fatt.AttivitaDettaglio d
        LEFT JOIN fatt.TipiDettaglioAttivita td
               ON td.IdTipoDettaglioAttivita = d.IdTipoDettaglioAttivita
        WHERE d.IdAttivitaDettaglio IN (
            SELECT DISTINCT r.IdAttivitaDettaglio
            FROM fatt.AvvisoFatturaRighe r
            WHERE r.IdAvviso = @IdAvviso AND r.IdAttivitaDettaglio IS NOT NULL)
        ORDER BY d.Ordine ASC;
        """;

    public async Task<IReadOnlyList<Models.DettaglioAvvisoGrandezze>> GetDettagliGrandezzeByAvvisoAsync(Guid idAvviso, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd  = new CommandDefinition(SqlDettagliGrandezze, new { IdAvviso = idAvviso }, cancellationToken: ct);
        var rows = await conn.QueryAsync<DettaglioGrandezzeRow>(cmd);
        return rows.Select(r => new Models.DettaglioAvvisoGrandezze(
            IdAttivitaDettaglio: r.IdAttivitaDettaglio,
            Tipo:                r.Tipo,
            Descrizione:         r.Descrizione,
            DataScadenza:        r.DataScadenza is { } d ? DateOnly.FromDateTime(d) : null,
            ImportoDettaglio:    r.ImportoDettaglio,
            AltriAvvisi:         r.AltriAvvisi,
            AvvisoAttuale:       r.AvvisoAttuale)).ToList();
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

    // IVA aggiornabile in modifica (il selettore Codice IVA resta disponibile);
    // AliquotaCnpaia/AliquotaRitenuta/ApplicaRitenuta NON sono qui: restano snapshot
    // congelati all'emissione (dipendono da aliquote di sistema e sostituto d'imposta).
    private const string SqlUpdate = """
        UPDATE fatt.AvvisiFattura SET
            DataAvviso               = @DataAvviso,
            Oggetto                  = @Oggetto,
            NotaSintetica            = @NotaSintetica,
            NotaTestata              = @NotaTestata,
            IdCodicePagamento        = @IdCodicePagamento,
            IdBancaAppoggio          = @IdBancaAppoggio,
            AliquotaIva              = @AliquotaIva,
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
    // Aggregate write — modifica dettagli (righe) di un avviso esistente
    // Stessa meccanica di lock dell'emissione, ma "replace-all": si sbloccano
    // le rate correnti, si eliminano le righe, si reinseriscono quelle nuove e
    // si ri-bloccano le rate delle righe reali sopravvissute.
    // =====================================================================

    public async Task AggiornaRigheAsync(
        Guid idAvviso,
        IReadOnlyList<AvvisoFatturaRiga> nuoveRighe,
        IReadOnlyList<Guid> idSpeseCollegate,
        CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        using var tx   = conn.BeginTransaction();
        try
        {
            var p = new { IdAvviso = idAvviso };

            // 1) Sblocca TUTTE le rate correnti dell'avviso (prima del delete: FK).
            await conn.ExecuteAsync(new CommandDefinition(SqlUnlockScadenze, p, transaction: tx, cancellationToken: ct));
            // 2) Elimina le vecchie righe (libera l'indice univoco sulle scadenze).
            await conn.ExecuteAsync(new CommandDefinition(SqlDeleteRighe, p, transaction: tx, cancellationToken: ct));
            // 3) Reinserisce le nuove righe (Ordine/IdRiga già assegnati dal manager).
            if (nuoveRighe.Count > 0)
                await conn.ExecuteAsync(new CommandDefinition(
                    SqlInsertRiga, nuoveRighe.Select(ToRigaParams), transaction: tx, cancellationToken: ct));

            // 4) Ri-blocca le rate delle righe reali sopravvissute.
            var lockParams = nuoveRighe
                .Where(r => r.IdScadenza.HasValue)
                .Select(r => new { IdAvvisoRiga = r.IdRiga, IdScadenza = r.IdScadenza!.Value })
                .ToList();
            if (lockParams.Count > 0)
                await conn.ExecuteAsync(new CommandDefinition(
                    SqlLockScadenza, lockParams, transaction: tx, cancellationToken: ct));

            // 5) Riconcilia le spese: scollega tutte quelle dell'avviso, poi ricollega
            //    le selezionate (SqlLinkSpesa aggancia solo le spese ancora libere).
            await conn.ExecuteAsync(new CommandDefinition(SqlUnlinkSpese, p, transaction: tx, cancellationToken: ct));
            if (idSpeseCollegate.Count > 0)
            {
                var spesaParams = idSpeseCollegate
                    .Select(id => new { IdAvviso = idAvviso, IdSpesaAnticipata = id })
                    .ToList();
                await conn.ExecuteAsync(new CommandDefinition(
                    SqlLinkSpesa, spesaParams, transaction: tx, cancellationToken: ct));
            }

            tx.Commit();
        }
        // Una rata risulta consumata da un altro avviso: indice univoco → dominio.
        catch (SqlException ex) when (ex.Number is 2627 or 2601)
        {
            tx.Rollback();
            throw new AvvisoFatturaInvalidaException(
                AvvisoFatturaMotivoInvalido.ScadenzaGiaInAvviso,
                "Una delle rate dell'avviso risulta ora in un altro avviso. Ricarica la maschera e riprova.");
        }
        catch
        {
            tx.Rollback();
            throw;
        }
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
