using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Repositories;

/// <summary>
/// Implementazione Dapper di <see cref="IMenuRepository"/>. Le colonne
/// "pagina Razor" si chiamano <c>Menu</c>/<c>SottoMenu</c> sul DB: la SELECT le
/// rinomina in <c>PaginaRazor</c> (proprietà dell'entità).
/// </summary>
internal sealed class MenuRepository : IMenuRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public MenuRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<Menu>> GetMenusAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        const string sql = """
            SELECT IdMenu, DescrizioneMenu, Menu AS PaginaRazor, Icona, Ordine, Attivo
            FROM fatt.Menu
            ORDER BY Ordine, DescrizioneMenu;
            """;
        var rows = await connection.QueryAsync<Menu>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<SottoMenu>> GetSottoMenusAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        const string sql = """
            SELECT IdSottoMenu, IdMenu, Descrizione, SottoMenu AS PaginaRazor, Icona, Ordine, Attivo
            FROM fatt.SottoMenu
            ORDER BY Ordine, Descrizione;
            """;
        var rows = await connection.QueryAsync<SottoMenu>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlySet<Guid>> GetIdMenuVisibiliAsync(Guid idRuolo, Guid idUtente, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        // Unione ruolo + override utente (un UNION elimina i duplicati).
        const string sql = """
            SELECT IdMenu FROM fatt.MenuRuolo  WHERE IdRuolo  = @IdRuolo
            UNION
            SELECT IdMenu FROM fatt.MenuUtente WHERE IdUtente = @IdUtente;
            """;
        var ids = await connection.QueryAsync<Guid>(new CommandDefinition(
            sql, new { IdRuolo = idRuolo, IdUtente = idUtente }, cancellationToken: cancellationToken));
        return ids.ToHashSet();
    }

    public async Task<IReadOnlySet<Guid>> GetIdSottoMenuVisibiliAsync(Guid idRuolo, Guid idUtente, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        const string sql = """
            SELECT IdSottoMenu FROM fatt.SottoMenuRuolo  WHERE IdRuolo  = @IdRuolo
            UNION
            SELECT IdSottoMenu FROM fatt.SottoMenuUtente WHERE IdUtente = @IdUtente;
            """;
        var ids = await connection.QueryAsync<Guid>(new CommandDefinition(
            sql, new { IdRuolo = idRuolo, IdUtente = idUtente }, cancellationToken: cancellationToken));
        return ids.ToHashSet();
    }

    // ---------------------------------------------------------------------
    // Configurazione mapping per ruolo (T3c)
    // ---------------------------------------------------------------------

    public async Task<IReadOnlySet<Guid>> GetMenuRuoloIdsAsync(Guid idRuolo, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var ids = await connection.QueryAsync<Guid>(new CommandDefinition(
            "SELECT IdMenu FROM fatt.MenuRuolo WHERE IdRuolo = @IdRuolo;",
            new { IdRuolo = idRuolo }, cancellationToken: cancellationToken));
        return ids.ToHashSet();
    }

    public async Task<IReadOnlySet<Guid>> GetSottoMenuRuoloIdsAsync(Guid idRuolo, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var ids = await connection.QueryAsync<Guid>(new CommandDefinition(
            "SELECT IdSottoMenu FROM fatt.SottoMenuRuolo WHERE IdRuolo = @IdRuolo;",
            new { IdRuolo = idRuolo }, cancellationToken: cancellationToken));
        return ids.ToHashSet();
    }

    public async Task SetMappingRuoloAsync(Guid idRuolo, IReadOnlyCollection<Guid> menuIds, IReadOnlyCollection<Guid> sottoMenuIds, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        // Sostituzione integrale: prima si azzera, poi si reinserisce. I GUID
        // delle righe di mapping sono generati app-side (UUIDv7).
        await connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM fatt.MenuRuolo      WHERE IdRuolo = @IdRuolo;
            DELETE FROM fatt.SottoMenuRuolo WHERE IdRuolo = @IdRuolo;
            """,
            new { IdRuolo = idRuolo }, cancellationToken: cancellationToken));

        if (menuIds.Count > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO fatt.MenuRuolo (IdMenuRuolo, IdMenu, IdRuolo) VALUES (@IdMenuRuolo, @IdMenu, @IdRuolo);",
                menuIds.Select(m => new { IdMenuRuolo = Guid.CreateVersion7(), IdMenu = m, IdRuolo = idRuolo }),
                cancellationToken: cancellationToken));
        }

        if (sottoMenuIds.Count > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO fatt.SottoMenuRuolo (IdSottoMenuRuolo, IdSottoMenu, IdRuolo) VALUES (@IdSottoMenuRuolo, @IdSottoMenu, @IdRuolo);",
                sottoMenuIds.Select(s => new { IdSottoMenuRuolo = Guid.CreateVersion7(), IdSottoMenu = s, IdRuolo = idRuolo }),
                cancellationToken: cancellationToken));
        }
    }
}
