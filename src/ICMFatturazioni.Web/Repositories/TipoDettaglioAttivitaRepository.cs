using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;
using Microsoft.Data.SqlClient;

namespace ICMFatturazioni.Web.Repositories;

/// <summary>
/// Implementazione Dapper di <see cref="ITipoDettaglioAttivitaRepository"/>.
/// La colonna SQL <c>TipoDettaglioAttivita</c> viene aliasata come <c>Descrizione</c>
/// in SELECT (CS0542: proprietà e classe non possono avere lo stesso nome).
/// HasDipendenzeAsync dipende da fatt.AttivitaDettaglio (migration 027).
/// </summary>
internal sealed class TipoDettaglioAttivitaRepository : ITipoDettaglioAttivitaRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public TipoDettaglioAttivitaRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    private sealed class TipoDettaglioRow
    {
        public Guid   IdTipoDettaglioAttivita { get; init; }
        public string Descrizione             { get; init; } = string.Empty;
        public bool   IsAttivo                { get; init; }
    }

    private static TipoDettaglioAttivita ToEntity(TipoDettaglioRow r) => new()
    {
        IdTipoDettaglioAttivita = r.IdTipoDettaglioAttivita,
        Descrizione             = r.Descrizione,
        IsAttivo                = r.IsAttivo,
    };

    private const string SqlSelect = """
        SELECT IdTipoDettaglioAttivita,
               TipoDettaglioAttivita AS Descrizione,
               IsAttivo
        FROM fatt.TipiDettaglioAttivita
        """;

    private const string SqlSelectAttivi = SqlSelect + " WHERE IsAttivo = 1 ORDER BY TipoDettaglioAttivita;";

    public async Task<IReadOnlyList<TipoDettaglioAttivita>> GetAttiviAsync(CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlSelectAttivi, cancellationToken: cancellationToken);
        var rows = await conn.QueryAsync<TipoDettaglioRow>(cmd);
        return rows.Select(ToEntity).ToList();
    }

    private const string SqlSelectById = SqlSelect + " WHERE IdTipoDettaglioAttivita = @IdTipoDettaglioAttivita;";

    public async Task<TipoDettaglioAttivita?> GetByIdAsync(Guid idTipoDettaglioAttivita, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlSelectById, new { IdTipoDettaglioAttivita = idTipoDettaglioAttivita }, cancellationToken: cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<TipoDettaglioRow>(cmd);
        return row is null ? null : ToEntity(row);
    }

    private const string SqlExistsDescr = """
        SELECT CASE WHEN EXISTS (
            SELECT 1 FROM fatt.TipiDettaglioAttivita
            WHERE IsAttivo = 1
              AND UPPER(TipoDettaglioAttivita) = UPPER(@Descrizione)
              AND (@EscludiId IS NULL OR IdTipoDettaglioAttivita <> @EscludiId)
        ) THEN 1 ELSE 0 END;
        """;

    public async Task<bool> ExistsDescrizioneAttivaAsync(string descrizione, Guid? escludiId, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlExistsDescr, new { Descrizione = descrizione, EscludiId = escludiId }, cancellationToken: cancellationToken);
        return await conn.ExecuteScalarAsync<bool>(cmd);
    }

    private const string SqlInsert = """
        INSERT INTO fatt.TipiDettaglioAttivita (IdTipoDettaglioAttivita, TipoDettaglioAttivita, IsAttivo)
        VALUES (@IdTipoDettaglioAttivita, @TipoDettaglioAttivita, @IsAttivo);
        """;

    public async Task InsertAsync(TipoDettaglioAttivita tipo, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlInsert, ToParams(tipo), cancellationToken: cancellationToken);
        await conn.ExecuteAsync(cmd);
    }

    private const string SqlUpdate = """
        UPDATE fatt.TipiDettaglioAttivita SET
            TipoDettaglioAttivita = @TipoDettaglioAttivita,
            IsAttivo              = @IsAttivo
        WHERE IdTipoDettaglioAttivita = @IdTipoDettaglioAttivita;
        """;

    public async Task UpdateAsync(TipoDettaglioAttivita tipo, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlUpdate, ToParams(tipo), cancellationToken: cancellationToken);
        await conn.ExecuteAsync(cmd);
    }

    private const string SqlDisattiva = "UPDATE fatt.TipiDettaglioAttivita SET IsAttivo = 0 WHERE IdTipoDettaglioAttivita = @IdTipoDettaglioAttivita;";

    public async Task DisattivaAsync(Guid idTipoDettaglioAttivita, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlDisattiva, new { IdTipoDettaglioAttivita = idTipoDettaglioAttivita }, cancellationToken: cancellationToken);
        await conn.ExecuteAsync(cmd);
    }

    // Dipende da fatt.AttivitaDettaglio creata in migration 027.
    private const string SqlHasDipendenze = """
        SELECT CASE WHEN EXISTS (
            SELECT 1 FROM fatt.AttivitaDettaglio
            WHERE IdTipoDettaglioAttivita = @IdTipoDettaglioAttivita
        ) THEN 1 ELSE 0 END;
        """;

    public async Task<bool> HasDipendenzeAsync(Guid idTipoDettaglioAttivita, CancellationToken cancellationToken = default)
    {
        try
        {
            using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            var cmd = new CommandDefinition(SqlHasDipendenze, new { IdTipoDettaglioAttivita = idTipoDettaglioAttivita }, cancellationToken: cancellationToken);
            return await conn.ExecuteScalarAsync<bool>(cmd);
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
            // fatt.AttivitaDettaglio non ancora creata (migration 027): nessuna dipendenza possibile.
            return false;
        }
    }

    // Mappa proprietà C# Descrizione → colonna SQL TipoDettaglioAttivita.
    private static object ToParams(TipoDettaglioAttivita t) => new
    {
        t.IdTipoDettaglioAttivita,
        TipoDettaglioAttivita = t.Descrizione,
        t.IsAttivo,
    };
}
