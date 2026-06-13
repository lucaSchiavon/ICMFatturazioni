using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Models;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Repositories;

/// <summary>
/// Implementazione Dapper di <see cref="IBancaAppoggioRepository"/>.
/// </summary>
/// <remarks>
/// Le letture fanno JOIN su <c>fatt.Banche</c> (INNER, la banca è obbligatoria)
/// e <c>fatt.Agenzie</c> (LEFT, la filiale è facoltativa) e mappano sul record
/// <see cref="BancaAppoggioRiga"/> per nome colonna (alias <c>BancaNome</c>/
/// <c>AgenziaNome</c>).
/// </remarks>
internal sealed class BancaAppoggioRepository : IBancaAppoggioRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public BancaAppoggioRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    // SELECT "ricca" condivisa (banca/agenzia risolte).
    private const string SqlSelectRiga = """
        SELECT
            ba.IdBancaAppoggio,
            ba.IdCliente,
            ba.IdBanca,
            b.Nome  AS BancaNome,
            b.ABI,
            ba.IdAgenzia,
            a.Nome  AS AgenziaNome,
            a.CAB,
            ba.IBAN,
            ba.IsAttivo
        FROM fatt.BancheAppoggio ba
        INNER JOIN fatt.Banche  b ON b.IdBanca   = ba.IdBanca
        LEFT  JOIN fatt.Agenzie a ON a.IdAgenzia = ba.IdAgenzia
        """;

    // ---------------------------------------------------------------------
    // Letture (modello ricco)
    // ---------------------------------------------------------------------

    private const string SqlSelectAttivi = SqlSelectRiga + """

        WHERE ba.IsAttivo = 1
        ORDER BY CASE WHEN ba.IdCliente IS NULL THEN 0 ELSE 1 END, b.Nome;
        """;

    public async Task<IReadOnlyList<BancaAppoggioRiga>> GetAttiviAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlSelectAttivi, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<BancaAppoggioRiga>(cmd);
        return rows.ToList();
    }

    private const string SqlSelectById = SqlSelectRiga + " WHERE ba.IdBancaAppoggio = @IdBancaAppoggio;";

    public async Task<BancaAppoggioRiga?> GetByIdAsync(Guid idBancaAppoggio, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlSelectById, new { IdBancaAppoggio = idBancaAppoggio }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<BancaAppoggioRiga>(cmd);
    }

    private const string SqlSelectSelezionabili = SqlSelectRiga + """

        WHERE ba.IsAttivo = 1
          AND (
                (@BancheAzienda = 1 AND ba.IdCliente IS NULL)
             OR (@BancheAzienda = 0 AND ba.IdCliente = @IdCliente)
              )
        ORDER BY b.Nome;
        """;

    public async Task<IReadOnlyList<BancaAppoggioRiga>> GetSelezionabiliAsync(Guid? idCliente, bool bancheAzienda, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlSelectSelezionabili,
            new { IdCliente = idCliente, BancheAzienda = bancheAzienda },
            cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<BancaAppoggioRiga>(cmd);
        return rows.ToList();
    }

    // ---------------------------------------------------------------------
    // Anti-duplicato legame (intestatario + banca + agenzia)
    // ---------------------------------------------------------------------

    private const string SqlExistsLegame = """
        SELECT CASE WHEN EXISTS (
            SELECT 1 FROM fatt.BancheAppoggio
            WHERE IsAttivo = 1
              AND IdBanca = @IdBanca
              AND IdAgenzia = @IdAgenzia
              AND ((@IdCliente IS NULL AND IdCliente IS NULL) OR IdCliente = @IdCliente)
              AND (@EscludiId IS NULL OR IdBancaAppoggio <> @EscludiId)
        ) THEN 1 ELSE 0 END;
        """;

    public async Task<bool> ExistsLegameAttivoAsync(Guid? idCliente, Guid idBanca, Guid? idAgenzia, Guid? escludiId, CancellationToken cancellationToken = default)
    {
        // Il vincolo si applica solo quando la filiale è indicata.
        if (idAgenzia is null)
        {
            return false;
        }
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlExistsLegame,
            new { IdCliente = idCliente, IdBanca = idBanca, IdAgenzia = idAgenzia, EscludiId = escludiId },
            cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(cmd);
    }

    // ---------------------------------------------------------------------
    // Scritture (entità di legame)
    // ---------------------------------------------------------------------

    private const string SqlInsert = """
        INSERT INTO fatt.BancheAppoggio (IdBancaAppoggio, IdCliente, IdBanca, IdAgenzia, IBAN, IsAttivo)
        VALUES (@IdBancaAppoggio, @IdCliente, @IdBanca, @IdAgenzia, @IBAN, @IsAttivo);
        """;

    public async Task InsertAsync(BancaAppoggio banca, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlInsert, ToParameters(banca), cancellationToken: cancellationToken);
        await connection.ExecuteAsync(cmd);
    }

    private const string SqlUpdate = """
        UPDATE fatt.BancheAppoggio SET
            IdCliente = @IdCliente,
            IdBanca   = @IdBanca,
            IdAgenzia = @IdAgenzia,
            IBAN      = @IBAN,
            IsAttivo  = @IsAttivo
        WHERE IdBancaAppoggio = @IdBancaAppoggio;
        """;

    public async Task UpdateAsync(BancaAppoggio banca, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlUpdate, ToParameters(banca), cancellationToken: cancellationToken);
        await connection.ExecuteAsync(cmd);
    }

    private const string SqlDisattiva = "UPDATE fatt.BancheAppoggio SET IsAttivo = 0 WHERE IdBancaAppoggio = @IdBancaAppoggio;";

    public async Task DisattivaAsync(Guid idBancaAppoggio, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlDisattiva, new { IdBancaAppoggio = idBancaAppoggio }, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(cmd);
    }

    private const string SqlHasDipendenze = """
        SELECT CASE WHEN EXISTS (
            SELECT 1 FROM fatt.Anagrafica
            WHERE IdBancaAppoggio = @IdBancaAppoggio AND IsAttivo = 1
        ) THEN 1 ELSE 0 END;
        """;

    public async Task<bool> HasDipendenzeAsync(Guid idBancaAppoggio, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlHasDipendenze, new { IdBancaAppoggio = idBancaAppoggio }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(cmd);
    }

    private static DynamicParameters ToParameters(BancaAppoggio b)
    {
        var p = new DynamicParameters();
        p.Add("IdBancaAppoggio", b.IdBancaAppoggio);
        p.Add("IdCliente",       b.IdCliente);
        p.Add("IdBanca",         b.IdBanca);
        p.Add("IdAgenzia",       b.IdAgenzia);
        p.Add("IBAN",            b.IBAN);
        p.Add("IsAttivo",        b.IsAttivo);
        return p;
    }
}
