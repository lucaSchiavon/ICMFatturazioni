using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Repositories;

/// <summary>
/// Implementazione Dapper di <see cref="IDescrizioneAttivitaRepository"/>.
/// Elenco ordinato per Ordine ASC, poi Descrizione ASC.
/// </summary>
internal sealed class DescrizioneAttivitaRepository : IDescrizioneAttivitaRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public DescrizioneAttivitaRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    private const string SqlSelect = """
        SELECT IdDescrizioneAttivita, Descrizione, Ordine, IsAttivo
        FROM fatt.DescrizioniAttivita
        """;

    private const string SqlSelectAttivi = SqlSelect + " WHERE IsAttivo = 1 ORDER BY Ordine, Descrizione;";

    public async Task<IReadOnlyList<DescrizioneAttivita>> GetAttiviAsync(CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlSelectAttivi, cancellationToken: cancellationToken);
        return (await conn.QueryAsync<DescrizioneAttivita>(cmd)).ToList();
    }

    private const string SqlSelectById = SqlSelect + " WHERE IdDescrizioneAttivita = @IdDescrizioneAttivita;";

    public async Task<DescrizioneAttivita?> GetByIdAsync(Guid idDescrizioneAttivita, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlSelectById, new { IdDescrizioneAttivita = idDescrizioneAttivita }, cancellationToken: cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<DescrizioneAttivita>(cmd);
    }

    private const string SqlExistsDescr = """
        SELECT CASE WHEN EXISTS (
            SELECT 1 FROM fatt.DescrizioniAttivita
            WHERE IsAttivo = 1
              AND UPPER(Descrizione) = UPPER(@Descrizione)
              AND (@EscludiId IS NULL OR IdDescrizioneAttivita <> @EscludiId)
        ) THEN 1 ELSE 0 END;
        """;

    public async Task<bool> ExistsDescrizioneAttivaAsync(string descrizione, Guid? escludiId, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlExistsDescr, new { Descrizione = descrizione, EscludiId = escludiId }, cancellationToken: cancellationToken);
        return await conn.ExecuteScalarAsync<bool>(cmd);
    }

    private const string SqlInsert = """
        INSERT INTO fatt.DescrizioniAttivita (IdDescrizioneAttivita, Descrizione, Ordine, IsAttivo)
        VALUES (@IdDescrizioneAttivita, @Descrizione, @Ordine, @IsAttivo);
        """;

    public async Task InsertAsync(DescrizioneAttivita descrizione, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlInsert, descrizione, cancellationToken: cancellationToken);
        await conn.ExecuteAsync(cmd);
    }

    private const string SqlUpdate = """
        UPDATE fatt.DescrizioniAttivita SET
            Descrizione = @Descrizione,
            Ordine      = @Ordine,
            IsAttivo    = @IsAttivo
        WHERE IdDescrizioneAttivita = @IdDescrizioneAttivita;
        """;

    public async Task UpdateAsync(DescrizioneAttivita descrizione, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlUpdate, descrizione, cancellationToken: cancellationToken);
        await conn.ExecuteAsync(cmd);
    }

    private const string SqlDisattiva = "UPDATE fatt.DescrizioniAttivita SET IsAttivo = 0 WHERE IdDescrizioneAttivita = @IdDescrizioneAttivita;";

    public async Task DisattivaAsync(Guid idDescrizioneAttivita, CancellationToken cancellationToken = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlDisattiva, new { IdDescrizioneAttivita = idDescrizioneAttivita }, cancellationToken: cancellationToken);
        await conn.ExecuteAsync(cmd);
    }
}
