using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;
using Microsoft.Data.SqlClient;

namespace ICMFatturazioni.Web.Repositories;

/// <summary>
/// Implementazione Dapper di <see cref="ITipoAttivitaRepository"/>.
/// La colonna SQL <c>TipoAttivita</c> viene aliasata come <c>Descrizione</c> in SELECT
/// per allinearsi alla proprietà dell'entità (CS0542: proprietà e classe non possono
/// avere lo stesso nome). GestisciCome (NVARCHAR 20) è convertito via Enum.Parse/ToString.
/// HasDipendenzeAsync dipende da fatt.Attivita (migration 026).
/// </summary>
internal sealed class TipoAttivitaRepository : ITipoAttivitaRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public TipoAttivitaRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    private sealed class TipoAttivitaRow
    {
        public Guid   IdTipoAttivita { get; init; }
        public string Descrizione    { get; init; } = string.Empty;
        public string GestisciCome   { get; init; } = string.Empty;
        public bool   StudiSettore   { get; init; }
        public bool   IsAttivo       { get; init; }
    }

    private static TipoAttivita ToEntity(TipoAttivitaRow r) => new()
    {
        IdTipoAttivita = r.IdTipoAttivita,
        Descrizione    = r.Descrizione,
        GestisciCome   = Enum.Parse<GestisciCome>(r.GestisciCome),
        StudiSettore   = r.StudiSettore,
        IsAttivo       = r.IsAttivo,
    };

    private const string SqlSelect = """
        SELECT IdTipoAttivita,
               TipoAttivita AS Descrizione,
               GestisciCome, StudiSettore, IsAttivo
        FROM fatt.TipiAttivita
        """;

    private const string SqlSelectAttivi = SqlSelect + " WHERE IsAttivo = 1 ORDER BY TipoAttivita;";

    public async Task<IReadOnlyList<TipoAttivita>> GetAttiviAsync(CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlSelectAttivi, cancellationToken: cancellationToken);
        var rows = await conn.QueryAsync<TipoAttivitaRow>(cmd);
        return rows.Select(ToEntity).ToList();
    }

    private const string SqlSelectById = SqlSelect + " WHERE IdTipoAttivita = @IdTipoAttivita;";

    public async Task<TipoAttivita?> GetByIdAsync(Guid idTipoAttivita, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlSelectById, new { IdTipoAttivita = idTipoAttivita }, cancellationToken: cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<TipoAttivitaRow>(cmd);
        return row is null ? null : ToEntity(row);
    }

    private const string SqlExistsDescr = """
        SELECT CASE WHEN EXISTS (
            SELECT 1 FROM fatt.TipiAttivita
            WHERE IsAttivo = 1
              AND UPPER(TipoAttivita) = UPPER(@Descrizione)
              AND (@EscludiId IS NULL OR IdTipoAttivita <> @EscludiId)
        ) THEN 1 ELSE 0 END;
        """;

    public async Task<bool> ExistsDescrizioneAttivaAsync(string descrizione, Guid? escludiId, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlExistsDescr, new { Descrizione = descrizione, EscludiId = escludiId }, cancellationToken: cancellationToken);
        return await conn.ExecuteScalarAsync<bool>(cmd);
    }

    private const string SqlInsert = """
        INSERT INTO fatt.TipiAttivita (IdTipoAttivita, TipoAttivita, GestisciCome, StudiSettore, IsAttivo)
        VALUES (@IdTipoAttivita, @TipoAttivita, @GestisciCome, @StudiSettore, @IsAttivo);
        """;

    public async Task InsertAsync(TipoAttivita tipo, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlInsert, ToParams(tipo), cancellationToken: cancellationToken);
        await conn.ExecuteAsync(cmd);
    }

    private const string SqlUpdate = """
        UPDATE fatt.TipiAttivita SET
            TipoAttivita = @TipoAttivita,
            GestisciCome = @GestisciCome,
            StudiSettore = @StudiSettore,
            IsAttivo     = @IsAttivo
        WHERE IdTipoAttivita = @IdTipoAttivita;
        """;

    public async Task UpdateAsync(TipoAttivita tipo, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlUpdate, ToParams(tipo), cancellationToken: cancellationToken);
        await conn.ExecuteAsync(cmd);
    }

    private const string SqlDisattiva = "UPDATE fatt.TipiAttivita SET IsAttivo = 0 WHERE IdTipoAttivita = @IdTipoAttivita;";

    public async Task DisattivaAsync(Guid idTipoAttivita, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlDisattiva, new { IdTipoAttivita = idTipoAttivita }, cancellationToken: cancellationToken);
        await conn.ExecuteAsync(cmd);
    }

    // Dipende da fatt.Attivita creata in migration 026.
    private const string SqlHasDipendenze = """
        SELECT CASE WHEN EXISTS (
            SELECT 1 FROM fatt.Attivita
            WHERE IdTipoAttivita = @IdTipoAttivita AND IsAttivo = 1
        ) THEN 1 ELSE 0 END;
        """;

    public async Task<bool> HasDipendenzeAsync(Guid idTipoAttivita, CancellationToken cancellationToken = default)
    {
        try
        {
            using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            var cmd = new CommandDefinition(SqlHasDipendenze, new { IdTipoAttivita = idTipoAttivita }, cancellationToken: cancellationToken);
            return await conn.ExecuteScalarAsync<bool>(cmd);
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
            // fatt.Attivita non ancora creata (migration 026): nessuna dipendenza possibile.
            return false;
        }
    }

    // Mappa proprietà C# Descrizione → colonna SQL TipoAttivita.
    private static object ToParams(TipoAttivita t) => new
    {
        t.IdTipoAttivita,
        TipoAttivita = t.Descrizione,
        GestisciCome = t.GestisciCome.ToString(),
        t.StudiSettore,
        t.IsAttivo,
    };
}
