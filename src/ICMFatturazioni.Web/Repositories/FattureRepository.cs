using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;
using ICMFatturazioni.Web.Models;
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
        public Guid      IdFattura           { get; init; }
        public Guid      IdAvviso            { get; init; }
        public int       NumeroFattura       { get; init; }
        public int       Anno                { get; init; }
        public DateTime  DataFattura         { get; init; }
        public bool      CreatoXML           { get; init; }
        public int       EsitoXML            { get; init; }
        public string?   ProgressivoInvio    { get; init; }
        public string?   NomeFileXml         { get; init; }
        public DateTime? DataCreazioneXmlUtc { get; init; }
        public DateTime? DataEsitoXmlUtc     { get; init; }
        public string?   Cig                 { get; init; }
        public string?   Cup                 { get; init; }
        public bool      IsAttivo            { get; init; }
    }

    private static Fattura ToEntity(FatturaRow r) => new()
    {
        IdFattura           = r.IdFattura,
        IdAvviso            = r.IdAvviso,
        NumeroFattura       = r.NumeroFattura,
        Anno                = r.Anno,
        DataFattura         = DateOnly.FromDateTime(r.DataFattura),
        CreatoXML           = r.CreatoXML,
        EsitoXML            = r.EsitoXML,
        ProgressivoInvio    = r.ProgressivoInvio,
        NomeFileXml         = r.NomeFileXml,
        DataCreazioneXmlUtc = r.DataCreazioneXmlUtc,
        DataEsitoXmlUtc     = r.DataEsitoXmlUtc,
        Cig                 = r.Cig,
        Cup                 = r.Cup,
        IsAttivo            = r.IsAttivo,
    };

    private const string SqlSelectBase = """
        SELECT IdFattura, IdAvviso, NumeroFattura, Anno, DataFattura,
               CreatoXML, EsitoXML, ProgressivoInvio, NomeFileXml,
               DataCreazioneXmlUtc, DataEsitoXmlUtc, Cig, Cup, IsAttivo
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
            (IdFattura, IdAvviso, NumeroFattura, Anno, DataFattura, CreatoXML, EsitoXML, Cig, Cup, IsAttivo)
        VALUES
            (@IdFattura, @IdAvviso, @NumeroFattura, @Anno, @DataFattura, @CreatoXML, @EsitoXML, @Cig, @Cup, @IsAttivo);
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
            fattura.Cig,
            fattura.Cup,
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

    // Sentinel `AND CreatoXML = 0`: una fattura con tracciato XML non si annulla
    // (va prima eliminato l'XML). Difende dalla race col pre-check del manager.
    private const string SqlAnnulla =
        "UPDATE fatt.Fatture SET IsAttivo = 0 WHERE IdFattura = @IdFattura AND CreatoXML = 0;";

    public async Task AnnullaAsync(Guid idFattura, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(SqlAnnulla, new { IdFattura = idFattura }, cancellationToken: ct);
        await conn.ExecuteAsync(cmd);
    }

    // Reset dello stato XML: riporta la fattura a "da creare" azzerando i metadati.
    // Sentinel `AND EsitoXML = 0`: non tocca le fatture con esito OK (protezione
    // sotto race col pre-check del manager). DataEsitoXmlUtc è già NULL se EsitoXML=0.
    private const string SqlResetXml = """
        UPDATE fatt.Fatture
        SET CreatoXML = 0,
            ProgressivoInvio    = NULL,
            NomeFileXml         = NULL,
            DataCreazioneXmlUtc = NULL
        WHERE IdFattura = @IdFattura AND IsAttivo = 1 AND EsitoXML = 0;
        """;

    public async Task ResetXmlAsync(Guid idFattura, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(SqlResetXml, new { IdFattura = idFattura }, cancellationToken: ct);
        await conn.ExecuteAsync(cmd);
    }

    // =====================================================================
    // Letture per la maschera "Stampe fatture"
    // =====================================================================

    // DTO Dapper: DataFattura come DateTime (colonna DATE) → convertita in DateOnly.
    private sealed class FatturaEmessaRow
    {
        public Guid     IdFattura               { get; init; }
        public Guid     IdAvviso                { get; init; }
        public int      NumeroFattura           { get; init; }
        public int      Anno                    { get; init; }
        public DateTime DataFattura             { get; init; }
        public string   ClienteRagioneSociale   { get; init; } = string.Empty;
        public string?  TipoAttivitaDescrizione { get; init; }
        public string   NumeroAttivita          { get; init; } = string.Empty;
        public string   DescrizioneAttivita     { get; init; } = string.Empty;
        public bool     CreatoXML               { get; init; }
        public int      EsitoXML                { get; init; }
    }

    // Fatture attive di un'attività, arricchite via l'avviso di origine con
    // cliente/tipo/attività (join sulle viste fatt.Anagrafica e fatt.Attivita).
    // Porta anche i flag XML (CreatoXML/EsitoXML): la UI ne ha bisogno per decidere
    // se la fattura è eliminabile (una con XML non lo è finché l'XML non è rimosso).
    private const string SqlEmesseByAttivita = """
        SELECT
            f.IdFattura, f.IdAvviso, f.NumeroFattura, f.Anno, f.DataFattura,
            an.RagioneSociale AS ClienteRagioneSociale,
            ta.TipoAttivita   AS TipoAttivitaDescrizione,
            att.Numero        AS NumeroAttivita,
            att.Descrizione   AS DescrizioneAttivita,
            f.CreatoXML, f.EsitoXML
        FROM fatt.Fatture f
        JOIN fatt.AvvisiFattura a   ON a.IdAvviso        = f.IdAvviso
        JOIN fatt.Attivita      att ON att.IdAttivita    = a.IdAttivita
        LEFT JOIN fatt.Anagrafica   an  ON an.IdAnagrafica  = a.IdAnagrafica
        LEFT JOIN fatt.TipiAttivita ta  ON ta.IdTipoAttivita = att.IdTipoAttivita
        WHERE f.IsAttivo = 1 AND a.IdAttivita = @IdAttivita
        ORDER BY f.Anno DESC, f.NumeroFattura DESC;
        """;

    public async Task<IReadOnlyList<FatturaEmessa>> GetEmesseByAttivitaAsync(Guid idAttivita, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd  = new CommandDefinition(SqlEmesseByAttivita, new { IdAttivita = idAttivita }, cancellationToken: ct);
        var rows = await conn.QueryAsync<FatturaEmessaRow>(cmd);
        return rows.Select(r => new FatturaEmessa(
            r.IdFattura, r.IdAvviso, r.NumeroFattura, r.Anno,
            DateOnly.FromDateTime(r.DataFattura),
            r.ClienteRagioneSociale, r.TipoAttivitaDescrizione,
            r.NumeroAttivita, r.DescrizioneAttivita,
            r.CreatoXML, r.EsitoXML)).ToList();
    }

    private const string SqlAnniConFatture = """
        SELECT DISTINCT Anno
        FROM fatt.Fatture
        WHERE IsAttivo = 1
        ORDER BY Anno DESC;
        """;

    public async Task<IReadOnlyList<int>> GetAnniConFattureAsync(CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd  = new CommandDefinition(SqlAnniConFatture, cancellationToken: ct);
        var anni = await conn.QueryAsync<int>(cmd);
        return anni.ToList();
    }

    // Coppie (cliente, attività) che hanno almeno una fattura attiva: restringe i
    // selettori della maschera ai soli clienti/attività fatturati.
    private const string SqlAttivitaConFatture = """
        SELECT DISTINCT a.IdAnagrafica, a.IdAttivita
        FROM fatt.Fatture f
        JOIN fatt.AvvisiFattura a ON a.IdAvviso = f.IdAvviso
        WHERE f.IsAttivo = 1;
        """;

    public async Task<IReadOnlyList<AttivitaFatturabile>> GetAttivitaConFattureAsync(CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd  = new CommandDefinition(SqlAttivitaConFatture, cancellationToken: ct);
        var rows = await conn.QueryAsync<AttivitaFatturabile>(cmd);
        return rows.ToList();
    }

    // =====================================================================
    // Fase D1 — maschera "Creazione-Gestione XML Documenti"
    // =====================================================================

    // DTO Dapper della griglia XML: TipoAnagrafica come CHAR(1) (string) →
    // convertito in enum a valle (come AnagraficaRepository).
    private sealed class DocumentoXmlRow
    {
        public Guid      IdFattura               { get; init; }
        public int       NumeroFattura           { get; init; }
        public int       Anno                    { get; init; }
        public DateTime  DataFattura             { get; init; }
        public string    TipoAnagrafica          { get; init; } = "P";
        public string    ClienteRagioneSociale   { get; init; } = string.Empty;
        public string?   TipoAttivitaDescrizione { get; init; }
        public string    NumeroAttivita          { get; init; } = string.Empty;
        public string    DescrizioneAttivita     { get; init; } = string.Empty;
        public bool      CreatoXML               { get; init; }
        public int       EsitoXML                { get; init; }
        public string?   ProgressivoInvio        { get; init; }
        public string?   NomeFileXml             { get; init; }
    }

    // Le clausole di stato sono composte con sentinelle @Creazione*/@Esito* che il
    // manager passa già risolte: niente concatenazione dinamica (Regola 5), un solo
    // testo SQL costante e parametrizzato che copre tutte le combinazioni del filtro.
    //   • Creazione: -1 = tutti · 0 = solo da creare · 1 = solo creato
    //   • Esito:     -1 = tutti · 0 = solo attesa   · 1 = solo OK
    private const string SqlPerXml = """
        SELECT
            f.IdFattura, f.NumeroFattura, f.Anno, f.DataFattura,
            an.TipoAnagrafica AS TipoAnagrafica,
            an.RagioneSociale AS ClienteRagioneSociale,
            ta.TipoAttivita   AS TipoAttivitaDescrizione,
            att.Numero        AS NumeroAttivita,
            att.Descrizione   AS DescrizioneAttivita,
            f.CreatoXML, f.EsitoXML, f.ProgressivoInvio, f.NomeFileXml
        FROM fatt.Fatture f
        JOIN fatt.AvvisiFattura a   ON a.IdAvviso        = f.IdAvviso
        JOIN fatt.Anagrafica    an  ON an.IdAnagrafica   = a.IdAnagrafica
        JOIN fatt.Attivita      att ON att.IdAttivita    = a.IdAttivita
        LEFT JOIN fatt.TipiAttivita ta ON ta.IdTipoAttivita = att.IdTipoAttivita
        WHERE f.IsAttivo = 1
          AND f.DataFattura >= @DataDa
          AND f.DataFattura <= @DataA
          AND (@IdAnagrafica IS NULL OR a.IdAnagrafica = @IdAnagrafica)
          AND (@CreazioneFlag = -1 OR CAST(f.CreatoXML AS INT) = @CreazioneFlag)
          AND (@EsitoFlag     = -1 OR f.EsitoXML             = @EsitoFlag)
        ORDER BY f.DataFattura DESC, f.NumeroFattura DESC;
        """;

    public async Task<IReadOnlyList<DocumentoXmlRiga>> GetPerXmlAsync(FiltroDocumentiXml filtro, CancellationToken ct = default)
    {
        var creazioneFlag = filtro.Creazione switch
        {
            StatoCreazioneXml.DaCreare => 0,
            StatoCreazioneXml.Creato   => 1,
            _                          => -1,
        };
        var esitoFlag = filtro.Esito switch
        {
            StatoEsitoXml.Attesa => 0,
            StatoEsitoXml.Ok     => 1,
            _                    => -1,
        };

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(SqlPerXml, new
        {
            DataDa        = ToSqlDate(filtro.DataDa),
            DataA         = ToSqlDate(filtro.DataA),
            filtro.IdAnagrafica,
            CreazioneFlag = creazioneFlag,
            EsitoFlag     = esitoFlag,
        }, cancellationToken: ct);

        var rows = await conn.QueryAsync<DocumentoXmlRow>(cmd);
        return rows.Select(r => new DocumentoXmlRiga(
            r.IdFattura, r.NumeroFattura, r.Anno,
            DateOnly.FromDateTime(r.DataFattura),
            TipoAnagraficaExtensions.FromDbCode(r.TipoAnagrafica[0]),
            r.ClienteRagioneSociale, r.TipoAttivitaDescrizione,
            r.NumeroAttivita, r.DescrizioneAttivita,
            r.CreatoXML, r.EsitoXML, r.ProgressivoInvio, r.NomeFileXml)).ToList();
    }

    public async Task<long> GetNextProgressivoInvioAsync(CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(
            "SELECT NEXT VALUE FOR fatt.SeqProgressivoInvio;", cancellationToken: ct);
        return await conn.ExecuteScalarAsync<long>(cmd);
    }

    private const string SqlSetXmlCreato = """
        UPDATE fatt.Fatture
        SET CreatoXML = 1,
            ProgressivoInvio    = @ProgressivoInvio,
            NomeFileXml         = @NomeFileXml,
            DataCreazioneXmlUtc = @CreatoUtc
        WHERE IdFattura = @IdFattura AND IsAttivo = 1;
        """;

    public async Task SetXmlCreatoAsync(Guid idFattura, string progressivoInvio, string nomeFileXml, DateTime creatoUtc, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(SqlSetXmlCreato, new
        {
            IdFattura = idFattura,
            ProgressivoInvio = progressivoInvio,
            NomeFileXml = nomeFileXml,
            CreatoUtc = creatoUtc,
        }, cancellationToken: ct);
        await conn.ExecuteAsync(cmd);
    }

    private const string SqlConfermaEsito = """
        UPDATE fatt.Fatture
        SET EsitoXML = 1, DataEsitoXmlUtc = @EsitoUtc
        WHERE IdFattura = @IdFattura AND IsAttivo = 1;
        """;

    public async Task ConfermaEsitoAsync(Guid idFattura, DateTime esitoUtc, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(SqlConfermaEsito,
            new { IdFattura = idFattura, EsitoUtc = esitoUtc }, cancellationToken: ct);
        await conn.ExecuteAsync(cmd);
    }

    private const string SqlTogliEsito = """
        UPDATE fatt.Fatture
        SET EsitoXML = 0, DataEsitoXmlUtc = NULL
        WHERE IdFattura = @IdFattura AND IsAttivo = 1;
        """;

    public async Task TogliEsitoAsync(Guid idFattura, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(SqlTogliEsito, new { IdFattura = idFattura }, cancellationToken: ct);
        await conn.ExecuteAsync(cmd);
    }
}
