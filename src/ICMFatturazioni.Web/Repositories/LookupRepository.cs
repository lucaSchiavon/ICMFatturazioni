using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Repositories;

/// <summary>
/// Lettura dei lookup <c>fatt.*</c> per i dropdown delle maschere.
/// Stateless, registrato singleton: i lookup non cambiano spesso e
/// vengono ri-letti ad ogni apertura del form (potenzialmente con
/// caching in futuro).
/// </summary>
internal sealed class LookupRepository : ILookupRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public LookupRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    // Italia in cima (ORDER BY (case when ... then 0 else 1 end)): è il
    // default applicativo per le anagrafiche, e mostrarlo in testa
    // riduce gli scroll inutili nei dropdown.
    private const string SqlPaesi = """
        SELECT CodicePaese AS Codice, Paese AS Descrizione
        FROM fatt.Paesi
        ORDER BY CASE WHEN CodicePaese = N'IT' THEN 0 ELSE 1 END, Paese;
        """;

    public async Task<IReadOnlyList<LookupItem>> GetPaesiAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlPaesi, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<LookupItem>(cmd);
        return rows.ToList();
    }

    private const string SqlProvince = """
        SELECT Prov AS Codice, Provincia AS Descrizione
        FROM fatt.Province
        WHERE Provincia IS NOT NULL
        ORDER BY Provincia;
        """;

    public async Task<IReadOnlyList<LookupItem>> GetProvinceAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlProvince, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<LookupItem>(cmd);
        return rows.ToList();
    }

    // Codice naturale (N1..N7) come valore di persistenza, ordinato per codice.
    private const string SqlNatureIVA = """
        SELECT Natura AS Codice, DescrizioneNatura AS Descrizione
        FROM fatt.NatureIVA
        ORDER BY Natura;
        """;

    public async Task<IReadOnlyList<LookupItem>> GetNatureIVAAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlNatureIVA, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<LookupItem>(cmd);
        return rows.ToList();
    }

    // NCHAR(4): si normalizza il codice con TRIM così il valore di persistenza
    // (TP01.., MP01..) non porta padding nel dropdown.
    private const string SqlCondizioniPagamento = """
        SELECT RTRIM(Codice) AS Codice, Descrizione
        FROM fatt.CondizioniPagamento
        ORDER BY Codice;
        """;

    public async Task<IReadOnlyList<LookupItem>> GetCondizioniPagamentoAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlCondizioniPagamento, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<LookupItem>(cmd);
        return rows.ToList();
    }

    private const string SqlModalitaPagamento = """
        SELECT RTRIM(Codice) AS Codice, Descrizione
        FROM fatt.ModalitaPagamento
        ORDER BY Codice;
        """;

    public async Task<IReadOnlyList<LookupItem>> GetModalitaPagamentoAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlModalitaPagamento, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<LookupItem>(cmd);
        return rows.ToList();
    }

    // Codelist fiscali AdE (migration 076): il valore di persistenza è il Codice.
    private const string SqlTipiCassa = """
        SELECT Codice, Descrizione FROM fatt.TipiCassa ORDER BY Codice;
        """;

    public async Task<IReadOnlyList<LookupItem>> GetTipiCassaAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlTipiCassa, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<LookupItem>(cmd);
        return rows.ToList();
    }

    private const string SqlTipiRitenuta = """
        SELECT Codice, Descrizione FROM fatt.TipiRitenuta ORDER BY Codice;
        """;

    public async Task<IReadOnlyList<LookupItem>> GetTipiRitenutaAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlTipiRitenuta, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<LookupItem>(cmd);
        return rows.ToList();
    }

    // Ordinamento per lunghezza+codice così le causali monocarattere (A, B, …)
    // precedono le bicarattere (L1, M1, ZO) invece di intercalarsi.
    private const string SqlCausaliPagamento = """
        SELECT Codice, Descrizione FROM fatt.CausaliPagamento ORDER BY LEN(Codice), Codice;
        """;

    public async Task<IReadOnlyList<LookupItem>> GetCausaliPagamentoAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlCausaliPagamento, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<LookupItem>(cmd);
        return rows.ToList();
    }
}
