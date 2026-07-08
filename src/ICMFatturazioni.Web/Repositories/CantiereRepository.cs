using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Repositories;

/// <summary>
/// Implementazione Dapper di <see cref="ICantiereRepository"/>.
/// Le query insistono sulla vista aggiornabile <c>fatt.Cantiere</c> (1:1 su
/// <c>dbo.Cantiere</c>, migration 071 di ICMVerbali): INSERT/UPDATE passano
/// attraverso la vista e gli invarianti (NOT NULL, FK_Cantiere_Progetto)
/// restano presidiati dalla tabella base.
/// </summary>
internal sealed class CantiereRepository : ICantiereRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public CantiereRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    // SELECT condivisa: definita una volta sola per non divergere fra le letture.
    private const string SqlSelectColumns = """
        SELECT IdCantiere, IdAttivita, Ubicazione, Tipologia, ImportoAppalto, IsAttivo
        FROM fatt.Cantiere
        """;

    // Solo gli attivi (soft-delete, ADR D22): i disattivati non compaiono in elenco.
    private const string SqlSelectAttivi = SqlSelectColumns + " WHERE IsAttivo = 1 ORDER BY Ubicazione;";

    public async Task<IReadOnlyList<Cantiere>> GetAttiviAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlSelectAttivi, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<Cantiere>(cmd);
        return rows.ToList();
    }

    // Cantieri attivi di una specifica attività: alimenta il terzo filtro della
    // maschera "Consultazione verbali".
    private const string SqlSelectByAttivita =
        SqlSelectColumns + " WHERE IdAttivita = @IdAttivita AND IsAttivo = 1 ORDER BY Ubicazione;";

    public async Task<IReadOnlyList<Cantiere>> GetByAttivitaAsync(Guid idAttivita, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(
            SqlSelectByAttivita,
            parameters: new { IdAttivita = idAttivita },
            cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<Cantiere>(cmd);
        return rows.ToList();
    }

    private const string SqlSelectById = SqlSelectColumns + " WHERE IdCantiere = @IdCantiere;";

    public async Task<Cantiere?> GetByIdAsync(Guid idCantiere, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(
            SqlSelectById,
            parameters: new { IdCantiere = idCantiere },
            cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Cantiere>(cmd);
    }

    // L'IdCantiere (GUID UUIDv7) arriva già valorizzato dal manager
    // (generazione app-side, ADR D22): niente IDENTITY/OUTPUT.
    private const string SqlInsert = """
        INSERT INTO fatt.Cantiere
            (IdCantiere, IdAttivita, Ubicazione, Tipologia, ImportoAppalto, IsAttivo)
        VALUES
            (@IdCantiere, @IdAttivita, @Ubicazione, @Tipologia, @ImportoAppalto, @IsAttivo);
        """;

    public async Task InsertAsync(Cantiere cantiere, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlInsert, parameters: cantiere, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(cmd);
    }

    private const string SqlUpdate = """
        UPDATE fatt.Cantiere SET
            IdAttivita     = @IdAttivita,
            Ubicazione     = @Ubicazione,
            Tipologia      = @Tipologia,
            ImportoAppalto = @ImportoAppalto,
            IsAttivo       = @IsAttivo
        WHERE IdCantiere = @IdCantiere;
        """;

    public async Task UpdateAsync(Cantiere cantiere, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlUpdate, parameters: cantiere, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(cmd);
    }

    // Soft-delete (ADR D22): disattiva, non rimuove fisicamente. Un DELETE fisico
    // potrebbe peraltro violare la FK dei verbali esistenti (dbo.Verbale.CantiereId).
    private const string SqlDisattiva = "UPDATE fatt.Cantiere SET IsAttivo = 0 WHERE IdCantiere = @IdCantiere;";

    public async Task DisattivaAsync(Guid idCantiere, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(
            SqlDisattiva,
            parameters: new { IdCantiere = idCantiere },
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(cmd);
    }
}
