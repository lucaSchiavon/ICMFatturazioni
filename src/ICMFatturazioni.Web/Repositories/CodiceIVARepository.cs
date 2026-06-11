using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Repositories;

/// <summary>
/// Implementazione Dapper di <see cref="ICodiceIVARepository"/>.
/// </summary>
/// <remarks>
/// I tipi delle colonne mappano direttamente sull'entità (niente DTO
/// intermedia come in Anagrafica: qui non ci sono <c>CHAR(1)</c>/enum da
/// disambiguare). <c>Aliquota DECIMAL(5,2)</c> ↔ <c>decimal</c>;
/// <c>ObbligoBollo BIT</c>/<c>IsAttivo BIT</c> ↔ <c>bool</c>.
/// </remarks>
internal sealed class CodiceIVARepository : ICodiceIVARepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public CodiceIVARepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    // SELECT condivisa: definita una volta sola per non divergere fra
    // GetAttivi e GetById. L'ordine delle colonne non conta (Dapper mappa per
    // nome), ma elenchiamo esplicitamente per chiarezza.
    private const string SqlSelectColumns = """
        SELECT
            IdCodiceIVA, Codice, Descrizione, Aliquota, Natura, ObbligoBollo, IsAttivo
        FROM fatt.CodiciIVA
        """;

    // ---------------------------------------------------------------------
    // Query: elenco attivi (ordinato per Codice)
    // ---------------------------------------------------------------------

    private const string SqlSelectAttivi = SqlSelectColumns + " WHERE IsAttivo = 1 ORDER BY Codice;";

    public async Task<IReadOnlyList<CodiceIVA>> GetAttiviAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlSelectAttivi, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<CodiceIVA>(cmd);
        return rows.ToList();
    }

    // ---------------------------------------------------------------------
    // Query: singolo record
    // ---------------------------------------------------------------------

    private const string SqlSelectById = SqlSelectColumns + " WHERE IdCodiceIVA = @IdCodiceIVA;";

    public async Task<CodiceIVA?> GetByIdAsync(Guid idCodiceIVA, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(
            SqlSelectById,
            parameters: new { IdCodiceIVA = idCodiceIVA },
            cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<CodiceIVA>(cmd);
    }

    // ---------------------------------------------------------------------
    // Query: esistenza sigla tra gli attivi (pre-check unicità)
    // ---------------------------------------------------------------------

    // Confronto case-insensitive: la collation di default di SQL Server è
    // CI, ma rendiamo esplicita l'intenzione per non dipendere dall'ambiente.
    private const string SqlExistsCodice = """
        SELECT CASE WHEN EXISTS (
            SELECT 1 FROM fatt.CodiciIVA
            WHERE IsAttivo = 1
              AND UPPER(Codice) = UPPER(@Codice)
              AND (@EscludiId IS NULL OR IdCodiceIVA <> @EscludiId)
        ) THEN 1 ELSE 0 END;
        """;

    public async Task<bool> ExistsCodiceAttivoAsync(string codice, Guid? escludiId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(
            SqlExistsCodice,
            parameters: new { Codice = codice, EscludiId = escludiId },
            cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(cmd);
    }

    // ---------------------------------------------------------------------
    // Comando: INSERT (Id già valorizzato dal manager, niente IDENTITY/OUTPUT)
    // ---------------------------------------------------------------------

    private const string SqlInsert = """
        INSERT INTO fatt.CodiciIVA
            (IdCodiceIVA, Codice, Descrizione, Aliquota, Natura, ObbligoBollo, IsAttivo)
        VALUES
            (@IdCodiceIVA, @Codice, @Descrizione, @Aliquota, @Natura, @ObbligoBollo, @IsAttivo);
        """;

    public async Task InsertAsync(CodiceIVA codiceIva, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlInsert, ToParameters(codiceIva), cancellationToken: cancellationToken);
        await connection.ExecuteAsync(cmd);
    }

    // ---------------------------------------------------------------------
    // Comando: UPDATE
    // ---------------------------------------------------------------------

    private const string SqlUpdate = """
        UPDATE fatt.CodiciIVA SET
            Codice       = @Codice,
            Descrizione  = @Descrizione,
            Aliquota     = @Aliquota,
            Natura       = @Natura,
            ObbligoBollo = @ObbligoBollo,
            IsAttivo     = @IsAttivo
        WHERE IdCodiceIVA = @IdCodiceIVA;
        """;

    public async Task UpdateAsync(CodiceIVA codiceIva, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlUpdate, ToParameters(codiceIva), cancellationToken: cancellationToken);
        await connection.ExecuteAsync(cmd);
    }

    // ---------------------------------------------------------------------
    // Comando: soft-delete
    // ---------------------------------------------------------------------

    private const string SqlDisattiva = "UPDATE fatt.CodiciIVA SET IsAttivo = 0 WHERE IdCodiceIVA = @IdCodiceIVA;";

    public async Task DisattivaAsync(Guid idCodiceIVA, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(
            SqlDisattiva,
            parameters: new { IdCodiceIVA = idCodiceIVA },
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(cmd);
    }

    // ---------------------------------------------------------------------
    // Query: dipendenze (anagrafiche attive che usano questo codice IVA)
    // ---------------------------------------------------------------------

    private const string SqlHasDipendenze = """
        SELECT CASE WHEN EXISTS (
            SELECT 1 FROM fatt.Anagrafica
            WHERE IdCodiciIVA = @IdCodiceIVA AND IsAttivo = 1
        ) THEN 1 ELSE 0 END;
        """;

    public async Task<bool> HasDipendenzeAsync(Guid idCodiceIVA, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(
            SqlHasDipendenze,
            parameters: new { IdCodiceIVA = idCodiceIVA },
            cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(cmd);
    }

    // ---------------------------------------------------------------------
    // Helper: traduce un CodiceIVA in parametri Dapper.
    // ---------------------------------------------------------------------

    private static DynamicParameters ToParameters(CodiceIVA c)
    {
        var p = new DynamicParameters();
        p.Add("IdCodiceIVA",  c.IdCodiceIVA);
        p.Add("Codice",       c.Codice);
        p.Add("Descrizione",  c.Descrizione);
        p.Add("Aliquota",     c.Aliquota);
        p.Add("Natura",       c.Natura);
        p.Add("ObbligoBollo", c.ObbligoBollo);
        p.Add("IsAttivo",     c.IsAttivo);
        return p;
    }
}
