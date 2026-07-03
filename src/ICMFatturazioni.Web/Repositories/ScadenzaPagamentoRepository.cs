using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Models;
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
        public Guid?    IdAvvisoRiga        { get; init; }
        public bool     IsAttivo            { get; init; }
        // Nav dell'avviso che ha evaso la rata (solo nella lettura per-dettaglio).
        public DateTime? AvvisoDataEvasione    { get; init; }
        public string?   AvvisoOggettoEvasione { get; init; }
    }

    private static ScadenzaPagamento ToEntity(ScadenzaPagamentoRow r) => new()
    {
        IdScadenza          = r.IdScadenza,
        IdAttivitaDettaglio = r.IdAttivitaDettaglio,
        DataScadenza        = DateOnly.FromDateTime(r.DataScadenza),
        Importo             = r.Importo,
        Nota                = r.Nota,
        IdAvvisoRiga        = r.IdAvvisoRiga,
        IsAttivo            = r.IsAttivo,
        AvvisoDataEvasione    = r.AvvisoDataEvasione is { } d ? DateOnly.FromDateTime(d) : null,
        AvvisoOggettoEvasione = r.AvvisoOggettoEvasione,
    };

    // DataScadenza: DATE NOT NULL in SQL → DateTime in ToEntity, DateTime? in params.
    private static DateTime? ToSqlDate(DateOnly d)
        => d.ToDateTime(TimeOnly.MinValue);

    private const string SqlSelectBase = """
        SELECT IdScadenza, IdAttivitaDettaglio, DataScadenza, Importo, Nota, IdAvvisoRiga, IsAttivo
        FROM fatt.SchedulazionePagamenti
        """;

    // Lettura per-dettaglio arricchita con l'avviso che ha evaso ciascuna rata
    // (data + oggetto), per mostrare in Gestione Scadenze il lock a livello rata.
    private const string SqlSelectByDettaglio = """
        SELECT s.IdScadenza, s.IdAttivitaDettaglio, s.DataScadenza, s.Importo, s.Nota,
               s.IdAvvisoRiga, s.IsAttivo,
               a.DataAvviso AS AvvisoDataEvasione,
               a.Oggetto    AS AvvisoOggettoEvasione
        FROM fatt.SchedulazionePagamenti s
        LEFT JOIN fatt.AvvisoFatturaRighe r ON r.IdRiga   = s.IdAvvisoRiga
        LEFT JOIN fatt.AvvisiFattura      a ON a.IdAvviso = r.IdAvviso AND a.IsAttivo = 1
        WHERE s.IdAttivitaDettaglio = @IdAttivitaDettaglio AND s.IsAttivo = 1
        ORDER BY s.DataScadenza ASC;
        """;

    public async Task<IReadOnlyList<ScadenzaPagamento>> GetByDettaglioAsync(Guid idAttivitaDettaglio, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd  = new CommandDefinition(SqlSelectByDettaglio, new { IdAttivitaDettaglio = idAttivitaDettaglio }, cancellationToken: ct);
        var rows = await conn.QueryAsync<ScadenzaPagamentoRow>(cmd);
        return rows.Select(ToEntity).ToList();
    }

    // -----------------------------------------------------------------------
    // Lettura "scadenze fatturabili" per attività (read-model per l'avviso).
    // Radicata sulla scadenza; JOIN read-only su dettaglio + tipo; subquery per
    // il "già allocato" (righe di avvisi attivi dello stesso dettaglio).
    // -----------------------------------------------------------------------
    private sealed class ScadenzaFatturabileRow
    {
        public Guid     IdScadenza                  { get; init; }
        public Guid     IdAttivitaDettaglio         { get; init; }
        public DateTime DataScadenza                { get; init; }
        public decimal  Importo                     { get; init; }
        public string?  Nota                        { get; init; }
        public int      OrdineDettaglio             { get; init; }
        public Guid     IdTipoDettaglioAttivita     { get; init; }
        public string?  TipoDettaglioDescrizione    { get; init; }
        public string   DescrizioneDettaglio        { get; init; } = string.Empty;
        public decimal  ImportoDettaglio            { get; init; }
        public decimal  GiaAllocatoAvvisiPrecedenti { get; init; }
    }

    private const string SqlSelectFatturabili = """
        SELECT
            s.IdScadenza,
            s.IdAttivitaDettaglio,
            s.DataScadenza,
            s.Importo,
            s.Nota,
            d.Ordine                  AS OrdineDettaglio,
            d.IdTipoDettaglioAttivita,
            td.TipoDettaglioAttivita  AS TipoDettaglioDescrizione,
            d.DescrizioneDettaglio,
            d.Importo                 AS ImportoDettaglio,
            (SELECT COALESCE(SUM(r.Importo), 0)
               FROM fatt.AvvisoFatturaRighe r
               JOIN fatt.AvvisiFattura a ON a.IdAvviso = r.IdAvviso
              WHERE r.IdAttivitaDettaglio = d.IdAttivitaDettaglio
                AND a.IsAttivo = 1
                -- In modifica, l'avviso corrente è escluso: le sue rate sono già
                -- ricaricate nella bozza, contarle qui le conterebbe due volte.
                AND (@IdAvvisoEscluso IS NULL OR a.IdAvviso <> @IdAvvisoEscluso)) AS GiaAllocatoAvvisiPrecedenti
        FROM fatt.SchedulazionePagamenti s
        JOIN fatt.AttivitaDettaglio d ON d.IdAttivitaDettaglio = s.IdAttivitaDettaglio
        LEFT JOIN fatt.TipiDettaglioAttivita td
               ON td.IdTipoDettaglioAttivita = d.IdTipoDettaglioAttivita
        WHERE d.IdAttivita = @IdAttivita
          AND s.IsAttivo = 1
          AND d.IsAttivo = 1
          AND s.IdAvvisoRiga IS NULL
        ORDER BY d.Ordine ASC, s.DataScadenza ASC;
        """;

    public async Task<IReadOnlyList<ScadenzaFatturabile>> GetFatturabiliByAttivitaAsync(Guid idAttivita, Guid? idAvvisoEscluso = null, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd  = new CommandDefinition(SqlSelectFatturabili, new { IdAttivita = idAttivita, IdAvvisoEscluso = idAvvisoEscluso }, cancellationToken: ct);
        var rows = await conn.QueryAsync<ScadenzaFatturabileRow>(cmd);
        return rows.Select(r => new ScadenzaFatturabile(
            IdScadenza:                  r.IdScadenza,
            IdAttivitaDettaglio:         r.IdAttivitaDettaglio,
            DataScadenza:                DateOnly.FromDateTime(r.DataScadenza),
            Importo:                     r.Importo,
            Nota:                        r.Nota,
            OrdineDettaglio:             r.OrdineDettaglio,
            IdTipoDettaglioAttivita:     r.IdTipoDettaglioAttivita,
            TipoDettaglioDescrizione:    r.TipoDettaglioDescrizione,
            DescrizioneDettaglio:        r.DescrizioneDettaglio,
            ImportoDettaglio:            r.ImportoDettaglio,
            GiaAllocatoAvvisiPrecedenti: r.GiaAllocatoAvvisiPrecedenti)).ToList();
    }

    // -----------------------------------------------------------------------
    // Attività con residuo da fatturare (per i filtri della maschera Avvisi).
    // Un'attività è fatturabile in due casi (UNION):
    //   1) Ha un dettaglio il cui importo eccede quanto già allocato in avvisi attivi
    //      (criterio basato sull'IMPORTO, non sull'esistenza di scadenze: un dettaglio
    //      senza scadenze — allocato 0 < importo — NON fa sparire l'attività).
    //   2) Ha almeno una spesa anticipata attiva non ancora messa in avviso: così è
    //      possibile emettere un avviso con SOLE spese art. 15, anche se le scadenze
    //      sono già tutte fatturate (o non ce ne sono).
    // Tolleranza 0,005 per assorbire eventuale rumore di arrotondamento.
    // -----------------------------------------------------------------------
    private const string SqlSelectAttivitaConResiduo = """
        SELECT DISTINCT a.IdAnagrafica, a.IdAttivita
        FROM fatt.Attivita a
        JOIN fatt.AttivitaDettaglio d ON d.IdAttivita = a.IdAttivita AND d.IsAttivo = 1
        WHERE a.IsAttivo = 1
          AND d.Importo > (
              SELECT COALESCE(SUM(r.Importo), 0)
              FROM fatt.AvvisoFatturaRighe r
              JOIN fatt.AvvisiFattura av ON av.IdAvviso = r.IdAvviso AND av.IsAttivo = 1
              WHERE r.IdAttivitaDettaglio = d.IdAttivitaDettaglio
          ) + 0.005

        UNION

        SELECT DISTINCT a.IdAnagrafica, a.IdAttivita
        FROM fatt.Attivita a
        JOIN fatt.SpeseAnticipate s ON s.IdAttivita = a.IdAttivita AND s.IsAttivo = 1 AND s.IdAvviso IS NULL
        WHERE a.IsAttivo = 1;
        """;

    public async Task<IReadOnlyList<AttivitaFatturabile>> GetAttivitaConResiduoDaFatturareAsync(CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd  = new CommandDefinition(SqlSelectAttivitaConResiduo, cancellationToken: ct);
        var rows = await conn.QueryAsync<AttivitaFatturabile>(cmd);
        return rows.ToList();
    }

    // -----------------------------------------------------------------------
    // Dettagli di un'attività non ancora interamente schedulati in scadenze
    // (somma rate attive < importo, incluso zero scadenze): la quota mancante non
    // è fatturabile finché non si pianificano le scadenze. Segnalati in maschera
    // Avvisi per non lasciare importi "invisibili".
    // -----------------------------------------------------------------------
    private const string SqlSelectDettagliNonSchedulati = """
        SELECT d.DescrizioneDettaglio AS Descrizione,
               d.Importo - COALESCE((
                   SELECT SUM(s.Importo) FROM fatt.SchedulazionePagamenti s
                   WHERE s.IdAttivitaDettaglio = d.IdAttivitaDettaglio AND s.IsAttivo = 1), 0) AS ImportoNonSchedulato
        FROM fatt.AttivitaDettaglio d
        WHERE d.IdAttivita = @IdAttivita
          AND d.IsAttivo = 1
          AND d.Importo > COALESCE((
                   SELECT SUM(s.Importo) FROM fatt.SchedulazionePagamenti s
                   WHERE s.IdAttivitaDettaglio = d.IdAttivitaDettaglio AND s.IsAttivo = 1), 0) + 0.005
        ORDER BY d.Ordine ASC;
        """;

    public async Task<IReadOnlyList<DettaglioDaSchedulare>> GetDettagliNonSchedulatiByAttivitaAsync(Guid idAttivita, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd  = new CommandDefinition(SqlSelectDettagliNonSchedulati, new { IdAttivita = idAttivita }, cancellationToken: ct);
        var rows = await conn.QueryAsync<DettaglioDaSchedulare>(cmd);
        return rows.ToList();
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

    // Sentinel di correttezza (doppia difesa, CLAUDE.md): una rata evasa da un
    // avviso (IdAvvisoRiga valorizzato) è congelata → l'UPDATE non tocca righe.
    private const string SqlUpdate = """
        UPDATE fatt.SchedulazionePagamenti SET
            DataScadenza = @DataScadenza,
            Importo      = @Importo,
            Nota         = @Nota
        WHERE IdScadenza = @IdScadenza AND IdAvvisoRiga IS NULL;
        """;

    public async Task UpdateAsync(ScadenzaPagamento scadenza, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(SqlUpdate, ToParams(scadenza), cancellationToken: ct);
        await conn.ExecuteAsync(cmd);
    }

    // Sentinel: non si soft-elimina una rata evasa (congelata finché l'avviso vive).
    private const string SqlDisattiva =
        "UPDATE fatt.SchedulazionePagamenti SET IsAttivo = 0 WHERE IdScadenza = @IdScadenza AND IdAvvisoRiga IS NULL;";

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
