using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;
using Microsoft.Data.SqlClient;

namespace ICMFatturazioni.Web.Repositories;

/// <summary>
/// Implementazione Dapper di <see cref="IAttivitaRepository"/>.
/// Le date sono mappate <c>DATE SQL ↔ DateOnly C#</c> tramite DTO intermedia
/// (Dapper su .NET 6+ supporta DateOnly natively).
/// Il campo <c>Numero</c> è inputtabile dall'utente (migration 027 ha rimosso l'IDENTITY).
/// HasDipendenzeAsync gestisce SQL error 208 (fatt.AttivitaDettaglio non ancora
/// creata) restituendo false finché la migration 028 non è applicata.
/// </summary>
internal sealed class AttivitaRepository : IAttivitaRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public AttivitaRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    // DTO: include le colonne di join (RagioneSociale, TipoAttivitaDescrizione).
    // Le colonne DATE vengono lette come DateTime? perché Dapper non mappa
    // automaticamente DATE SQL → DateOnly C# (lo fa in scrittura via ToSqlDate,
    // in lettura si converte esplicitamente in ToEntity).
    private sealed class AttivitaRow
    {
        public Guid      IdAttivita              { get; init; }
        public Guid      IdAnagrafica            { get; init; }
        public Guid      IdTipoAttivita          { get; init; }
        public string    Numero                  { get; init; } = string.Empty;
        public string    Descrizione             { get; init; } = string.Empty;
        public DateTime? ProgettoDefinitivo      { get; init; }
        public DateTime? ConcessioneEdilizia     { get; init; }
        public DateTime? InizioLavori            { get; init; }
        public decimal?  ImportoOpera            { get; init; }
        public bool      IsAttivo                { get; init; }
        // Join di convenienza
        public string?   RagioneSociale          { get; init; }
        public string?   TipoAttivitaDescrizione { get; init; }
    }

    private static DateOnly? FromSqlDate(DateTime? dt)
        => dt.HasValue ? DateOnly.FromDateTime(dt.Value) : null;

    private static Attivita ToEntity(AttivitaRow r) => new()
    {
        IdAttivita              = r.IdAttivita,
        IdAnagrafica            = r.IdAnagrafica,
        IdTipoAttivita          = r.IdTipoAttivita,
        Numero                  = r.Numero,
        Descrizione             = r.Descrizione,
        ProgettoDefinitivo      = FromSqlDate(r.ProgettoDefinitivo),
        ConcessioneEdilizia     = FromSqlDate(r.ConcessioneEdilizia),
        InizioLavori            = FromSqlDate(r.InizioLavori),
        ImportoOpera            = r.ImportoOpera,
        IsAttivo                = r.IsAttivo,
        RagioneSociale          = r.RagioneSociale,
        TipoAttivitaDescrizione = r.TipoAttivitaDescrizione,
    };

    private const string SqlSelectBase = """
        SELECT a.IdAttivita, a.IdAnagrafica, a.IdTipoAttivita, a.Numero,
               a.Descrizione, a.ProgettoDefinitivo, a.ConcessioneEdilizia,
               a.InizioLavori, a.ImportoOpera, a.IsAttivo,
               an.RagioneSociale,
               ta.TipoAttivita AS TipoAttivitaDescrizione
        FROM fatt.Attivita a
        LEFT JOIN fatt.Anagrafica   an ON an.IdAnagrafica  = a.IdAnagrafica
        LEFT JOIN fatt.TipiAttivita ta ON ta.IdTipoAttivita = a.IdTipoAttivita
        """;

    private const string SqlSelectAttivi =
        SqlSelectBase + " WHERE a.IsAttivo = 1 ORDER BY TRY_CAST(a.Numero AS int) DESC, a.Numero DESC;";

    public async Task<IReadOnlyList<Attivita>> GetAttiviAsync(CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd  = new CommandDefinition(SqlSelectAttivi, cancellationToken: cancellationToken);
        var rows = await conn.QueryAsync<AttivitaRow>(cmd);
        return rows.Select(ToEntity).ToList();
    }

    private const string SqlSelectByAnagrafica =
        SqlSelectBase + " WHERE a.IdAnagrafica = @IdAnagrafica AND a.IsAttivo = 1 ORDER BY TRY_CAST(a.Numero AS int) DESC, a.Numero DESC;";

    public async Task<IReadOnlyList<Attivita>> GetByAnagraficaAsync(Guid idAnagrafica, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd  = new CommandDefinition(SqlSelectByAnagrafica, new { IdAnagrafica = idAnagrafica }, cancellationToken: cancellationToken);
        var rows = await conn.QueryAsync<AttivitaRow>(cmd);
        return rows.Select(ToEntity).ToList();
    }

    private const string SqlSelectByAnagraficaETipo =
        SqlSelectBase + " WHERE a.IdAnagrafica = @IdAnagrafica AND a.IdTipoAttivita = @IdTipoAttivita AND a.IsAttivo = 1 ORDER BY TRY_CAST(a.Numero AS int) DESC, a.Numero DESC;";

    public async Task<IReadOnlyList<Attivita>> GetByAnagraficaETipoAsync(Guid idAnagrafica, Guid idTipoAttivita, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd  = new CommandDefinition(SqlSelectByAnagraficaETipo,
            new { IdAnagrafica = idAnagrafica, IdTipoAttivita = idTipoAttivita },
            cancellationToken: cancellationToken);
        var rows = await conn.QueryAsync<AttivitaRow>(cmd);
        return rows.Select(ToEntity).ToList();
    }

    private const string SqlSelectById =
        SqlSelectBase + " WHERE a.IdAttivita = @IdAttivita;";

    public async Task<Attivita?> GetByIdAsync(Guid idAttivita, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlSelectById, new { IdAttivita = idAttivita }, cancellationToken: cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<AttivitaRow>(cmd);
        return row is null ? null : ToEntity(row);
    }

    // Numero è inputtabile dall'utente (migration 027 ha rimosso l'IDENTITY).
    private const string SqlInsert = """
        INSERT INTO fatt.Attivita
            (IdAttivita, IdAnagrafica, IdTipoAttivita, Numero, Descrizione,
             ProgettoDefinitivo, ConcessioneEdilizia, InizioLavori,
             ImportoOpera, IsAttivo)
        VALUES
            (@IdAttivita, @IdAnagrafica, @IdTipoAttivita, @Numero, @Descrizione,
             @ProgettoDefinitivo, @ConcessioneEdilizia, @InizioLavori,
             @ImportoOpera, @IsAttivo);
        """;

    public async Task InsertAsync(Attivita attivita, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlInsert, ToParams(attivita), cancellationToken: cancellationToken);
        await conn.ExecuteAsync(cmd);
    }

    private const string SqlUpdate = """
        UPDATE fatt.Attivita SET
            IdAnagrafica        = @IdAnagrafica,
            IdTipoAttivita      = @IdTipoAttivita,
            Numero              = @Numero,
            Descrizione         = @Descrizione,
            ProgettoDefinitivo  = @ProgettoDefinitivo,
            ConcessioneEdilizia = @ConcessioneEdilizia,
            InizioLavori        = @InizioLavori,
            ImportoOpera        = @ImportoOpera,
            IsAttivo            = @IsAttivo
        WHERE IdAttivita = @IdAttivita;
        """;

    public async Task UpdateAsync(Attivita attivita, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlUpdate, ToParams(attivita), cancellationToken: cancellationToken);
        await conn.ExecuteAsync(cmd);
    }

    private const string SqlDisattiva =
        "UPDATE fatt.Attivita SET IsAttivo = 0 WHERE IdAttivita = @IdAttivita;";

    public async Task DisattivaAsync(Guid idAttivita, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlDisattiva, new { IdAttivita = idAttivita }, cancellationToken: cancellationToken);
        await conn.ExecuteAsync(cmd);
    }

    // Dipende da fatt.AttivitaDettaglio creata in migration 027.
    private const string SqlHasDipendenze = """
        SELECT CASE WHEN EXISTS (
            SELECT 1 FROM fatt.AttivitaDettaglio
            WHERE IdAttivita = @IdAttivita
        ) THEN 1 ELSE 0 END;
        """;

    public async Task<bool> HasDipendenzeAsync(Guid idAttivita, CancellationToken cancellationToken = default)
    {
        try
        {
            using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            var cmd = new CommandDefinition(SqlHasDipendenze, new { IdAttivita = idAttivita }, cancellationToken: cancellationToken);
            return await conn.ExecuteScalarAsync<bool>(cmd);
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
            // fatt.AttivitaDettaglio non ancora creata (migration 028).
            return false;
        }
    }

    // Dapper mappa DateOnly ↔ DATE in lettura ma NON in scrittura:
    // i parametri devono essere passati come DateTime? (o DbType.Date esplicito).
    private static DateTime? ToSqlDate(DateOnly? d)
        => d.HasValue ? d.Value.ToDateTime(TimeOnly.MinValue) : null;

    private static object ToParams(Attivita a) => new
    {
        a.IdAttivita,
        a.IdAnagrafica,
        a.IdTipoAttivita,
        a.Numero,
        a.Descrizione,
        ProgettoDefinitivo  = ToSqlDate(a.ProgettoDefinitivo),
        ConcessioneEdilizia = ToSqlDate(a.ConcessioneEdilizia),
        InizioLavori        = ToSqlDate(a.InizioLavori),
        a.ImportoOpera,
        a.IsAttivo,
    };
}
