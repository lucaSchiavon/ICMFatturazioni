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
}
