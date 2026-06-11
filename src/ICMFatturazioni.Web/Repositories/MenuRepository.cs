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
            SELECT IdMenu, DescrizioneMenu, Menu AS PaginaRazor, Icona, Ordine, Attivo, SoloAdmin
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
            SELECT IdSottoMenu, IdMenu, Descrizione, SottoMenu AS PaginaRazor, Icona, Ordine, Attivo, SoloSuperadmin
            FROM fatt.SottoMenu
            ORDER BY Ordine, Descrizione;
            """;
        var rows = await connection.QueryAsync<SottoMenu>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    // ---------------------------------------------------------------------
    // Mapping per ruolo (T3c)
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

    // ---------------------------------------------------------------------
    // Mapping per utente / override (T3d)
    // ---------------------------------------------------------------------

    public async Task<IReadOnlySet<Guid>> GetMenuUtenteIdsAsync(Guid idUtente, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var ids = await connection.QueryAsync<Guid>(new CommandDefinition(
            "SELECT IdMenu FROM fatt.MenuUtente WHERE IdUtente = @IdUtente;",
            new { IdUtente = idUtente }, cancellationToken: cancellationToken));
        return ids.ToHashSet();
    }

    public async Task<IReadOnlySet<Guid>> GetSottoMenuUtenteIdsAsync(Guid idUtente, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var ids = await connection.QueryAsync<Guid>(new CommandDefinition(
            "SELECT IdSottoMenu FROM fatt.SottoMenuUtente WHERE IdUtente = @IdUtente;",
            new { IdUtente = idUtente }, cancellationToken: cancellationToken));
        return ids.ToHashSet();
    }

    public async Task SetMappingUtenteAsync(Guid idUtente, IReadOnlyCollection<Guid> menuIds, IReadOnlyCollection<Guid> sottoMenuIds, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        // Sostituzione integrale dell'override. Insiemi vuoti → personalizzazione rimossa.
        await connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM fatt.MenuUtente      WHERE IdUtente = @IdUtente;
            DELETE FROM fatt.SottoMenuUtente WHERE IdUtente = @IdUtente;
            """,
            new { IdUtente = idUtente }, cancellationToken: cancellationToken));

        if (menuIds.Count > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO fatt.MenuUtente (IdMenuUtente, IdMenu, IdUtente) VALUES (@IdMenuUtente, @IdMenu, @IdUtente);",
                menuIds.Select(m => new { IdMenuUtente = Guid.CreateVersion7(), IdMenu = m, IdUtente = idUtente }),
                cancellationToken: cancellationToken));
        }

        if (sottoMenuIds.Count > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO fatt.SottoMenuUtente (IdSottoMenuUtente, IdSottoMenu, IdUtente) VALUES (@IdSottoMenuUtente, @IdSottoMenu, @IdUtente);",
                sottoMenuIds.Select(s => new { IdSottoMenuUtente = Guid.CreateVersion7(), IdSottoMenu = s, IdUtente = idUtente }),
                cancellationToken: cancellationToken));
        }
    }
}
