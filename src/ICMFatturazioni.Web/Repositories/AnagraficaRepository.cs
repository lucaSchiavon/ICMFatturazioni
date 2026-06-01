using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Repositories;

/// <summary>
/// Implementazione Dapper di <see cref="IAnagraficaRepository"/>.
/// </summary>
/// <remarks>
/// La colonna <c>TipoAnagrafica CHAR(1)</c> non è mappata direttamente
/// sull'enum <see cref="TipoAnagrafica"/>: Dapper convertirebbe il char
/// nel suo ordinale UTF-16 (es. <c>'S'</c> → 83) anziché nel valore enum
/// inteso (<c>TipoAnagrafica.Societa = 'S' = 83</c> — funzionerebbe per
/// caso ma per ragioni opache). Per evitare ambiguità usiamo una DTO
/// intermedia (<see cref="AnagraficaRow"/>) con <c>string TipoAnagrafica</c>
/// e mappiamo a mano nelle estrazioni.
/// </remarks>
internal sealed class AnagraficaRepository : IAnagraficaRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public AnagraficaRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    // ---------------------------------------------------------------------
    // DTO interna: mappa esatta delle colonne nella SELECT (TipoAnagrafica
    // come string). Non esce mai dal repository.
    // ---------------------------------------------------------------------
    private sealed class AnagraficaRow
    {
        public int IdAnagrafica { get; init; }
        public string TipoAnagrafica { get; init; } = string.Empty;
        public string RagioneSociale { get; init; } = string.Empty;
        public string? Indirizzo { get; init; }
        public string? CAP { get; init; }
        public string? City { get; init; }
        public string? Provincia { get; init; }
        public string SiglaPaese { get; init; } = "IT";
        public string? Telefono { get; init; }
        public string? Cellulare { get; init; }
        public string? Fax { get; init; }
        public string? Email { get; init; }
        public string? PIVA { get; init; }
        public string? Contatto { get; init; }
        public int? IdPag { get; init; }
        public int? IdBancaAppoggio { get; init; }
        public int? IdCodiciIVA { get; init; }
        public int? IdTipologieClientela { get; init; }
        public string? CodiceDestinatario { get; init; }
        public string? PECFatturaElettronica { get; init; }
        public DateTime DataRecord { get; init; }
    }

    private static Anagrafica ToEntity(AnagraficaRow row) => new()
    {
        IdAnagrafica          = row.IdAnagrafica,
        TipoAnagrafica        = TipoAnagraficaExtensions.FromDbCode(row.TipoAnagrafica[0]),
        RagioneSociale        = row.RagioneSociale,
        Indirizzo             = row.Indirizzo,
        CAP                   = row.CAP,
        City                  = row.City,
        Provincia             = row.Provincia,
        SiglaPaese            = row.SiglaPaese,
        Telefono              = row.Telefono,
        Cellulare             = row.Cellulare,
        Fax                   = row.Fax,
        Email                 = row.Email,
        PIVA                  = row.PIVA,
        Contatto              = row.Contatto,
        IdPag                 = row.IdPag,
        IdBancaAppoggio       = row.IdBancaAppoggio,
        IdCodiciIVA           = row.IdCodiciIVA,
        IdTipologieClientela  = row.IdTipologieClientela,
        CodiceDestinatario    = row.CodiceDestinatario,
        PECFatturaElettronica = row.PECFatturaElettronica,
        DataRecord            = row.DataRecord,
    };

    // SELECT condivisa: definita una volta sola per non divergere fra
    // GetAll e GetById. Aggiungere campi qui equivale ad aggiungerli a
    // tutte le query di lettura.
    private const string SqlSelectColumns = """
        SELECT
            IdAnagrafica, TipoAnagrafica, RagioneSociale, Indirizzo, CAP, City,
            Provincia, SiglaPaese, Telefono, Cellulare, Fax, Email, PIVA,
            Contatto, IdPag, IdBancaAppoggio, IdCodiciIVA, IdTipologieClientela,
            CodiceDestinatario, PECFatturaElettronica, DataRecord
        FROM ana.Anagrafica
        """;

    // ---------------------------------------------------------------------
    // Query: elenco completo (ordinato per RagioneSociale)
    // ---------------------------------------------------------------------

    private const string SqlSelectAll = SqlSelectColumns + " ORDER BY RagioneSociale;";

    public async Task<IReadOnlyList<Anagrafica>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlSelectAll, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<AnagraficaRow>(cmd);
        return rows.Select(ToEntity).ToList();
    }

    // ---------------------------------------------------------------------
    // Query: singolo record
    // ---------------------------------------------------------------------

    private const string SqlSelectById = SqlSelectColumns + " WHERE IdAnagrafica = @IdAnagrafica;";

    public async Task<Anagrafica?> GetByIdAsync(int idAnagrafica, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(
            SqlSelectById,
            parameters: new { IdAnagrafica = idAnagrafica },
            cancellationToken: cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<AnagraficaRow>(cmd);
        return row is null ? null : ToEntity(row);
    }

    // ---------------------------------------------------------------------
    // Comando: INSERT con OUTPUT
    // ---------------------------------------------------------------------

    private const string SqlInsert = """
        INSERT INTO ana.Anagrafica
            (TipoAnagrafica, RagioneSociale, Indirizzo, CAP, City, Provincia,
             SiglaPaese, Telefono, Cellulare, Fax, Email, PIVA, Contatto,
             IdPag, IdBancaAppoggio, IdCodiciIVA, IdTipologieClientela,
             CodiceDestinatario, PECFatturaElettronica)
        OUTPUT INSERTED.IdAnagrafica
        VALUES
            (@TipoAnagrafica, @RagioneSociale, @Indirizzo, @CAP, @City, @Provincia,
             @SiglaPaese, @Telefono, @Cellulare, @Fax, @Email, @PIVA, @Contatto,
             @IdPag, @IdBancaAppoggio, @IdCodiciIVA, @IdTipologieClientela,
             @CodiceDestinatario, @PECFatturaElettronica);
        """;

    public async Task<int> InsertAsync(Anagrafica anagrafica, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(
            SqlInsert,
            parameters: ToParameters(anagrafica),
            cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(cmd);
    }

    // ---------------------------------------------------------------------
    // Comando: UPDATE
    // ---------------------------------------------------------------------

    private const string SqlUpdate = """
        UPDATE ana.Anagrafica SET
            TipoAnagrafica        = @TipoAnagrafica,
            RagioneSociale        = @RagioneSociale,
            Indirizzo             = @Indirizzo,
            CAP                   = @CAP,
            City                  = @City,
            Provincia             = @Provincia,
            SiglaPaese            = @SiglaPaese,
            Telefono              = @Telefono,
            Cellulare             = @Cellulare,
            Fax                   = @Fax,
            Email                 = @Email,
            PIVA                  = @PIVA,
            Contatto              = @Contatto,
            IdPag                 = @IdPag,
            IdBancaAppoggio       = @IdBancaAppoggio,
            IdCodiciIVA           = @IdCodiciIVA,
            IdTipologieClientela  = @IdTipologieClientela,
            CodiceDestinatario    = @CodiceDestinatario,
            PECFatturaElettronica = @PECFatturaElettronica
        WHERE IdAnagrafica = @IdAnagrafica;
        """;

    public async Task UpdateAsync(Anagrafica anagrafica, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var parameters = ToParameters(anagrafica);
        parameters.Add("IdAnagrafica", anagrafica.IdAnagrafica);
        var cmd = new CommandDefinition(SqlUpdate, parameters, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(cmd);
    }

    // ---------------------------------------------------------------------
    // Comando: DELETE
    // ---------------------------------------------------------------------

    private const string SqlDelete = "DELETE FROM ana.Anagrafica WHERE IdAnagrafica = @IdAnagrafica;";

    public async Task DeleteAsync(int idAnagrafica, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(
            SqlDelete,
            parameters: new { IdAnagrafica = idAnagrafica },
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(cmd);
    }

    // ---------------------------------------------------------------------
    // Query: dipendenze (placeholder Fase 2)
    // ---------------------------------------------------------------------

    public Task<bool> HasDipendenzeAsync(int idAnagrafica, CancellationToken cancellationToken = default)
    {
        // In Fase 2 non esistono tabelle a valle (Progetti, Avvisi, Fatture).
        // Quando entreranno, la query diventerà tipicamente un UNION ALL di
        // EXISTS sulle tabelle dipendenti, sotto un singolo SELECT TOP 1.
        _ = idAnagrafica;
        _ = cancellationToken;
        return Task.FromResult(false);
    }

    // ---------------------------------------------------------------------
    // Helper: traduce un Anagrafica in DynamicParameters per @ bind Dapper.
    // ---------------------------------------------------------------------

    private static DynamicParameters ToParameters(Anagrafica a)
    {
        var p = new DynamicParameters();
        // TipoAnagrafica passato come char (CHAR(1) DB). Dapper riconosce
        // char e lo invia come stringa di 1 carattere.
        p.Add("TipoAnagrafica",        a.TipoAnagrafica.ToDbCode());
        p.Add("RagioneSociale",        a.RagioneSociale);
        p.Add("Indirizzo",             a.Indirizzo);
        p.Add("CAP",                   a.CAP);
        p.Add("City",                  a.City);
        p.Add("Provincia",             a.Provincia);
        p.Add("SiglaPaese",            a.SiglaPaese);
        p.Add("Telefono",              a.Telefono);
        p.Add("Cellulare",             a.Cellulare);
        p.Add("Fax",                   a.Fax);
        p.Add("Email",                 a.Email);
        p.Add("PIVA",                  a.PIVA);
        p.Add("Contatto",              a.Contatto);
        p.Add("IdPag",                 a.IdPag);
        p.Add("IdBancaAppoggio",       a.IdBancaAppoggio);
        p.Add("IdCodiciIVA",           a.IdCodiciIVA);
        p.Add("IdTipologieClientela",  a.IdTipologieClientela);
        p.Add("CodiceDestinatario",    a.CodiceDestinatario);
        p.Add("PECFatturaElettronica", a.PECFatturaElettronica);
        return p;
    }
}
