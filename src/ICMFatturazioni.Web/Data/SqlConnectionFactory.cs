using System.Data;
using Microsoft.Data.SqlClient;

namespace ICMFatturazioni.Web.Data;

/// <summary>
/// Implementazione di <see cref="ISqlConnectionFactory"/> basata su
/// <c>Microsoft.Data.SqlClient</c>. Stateless: la connection string è
/// risolta una sola volta dal costruttore tramite <see cref="IConfiguration"/>,
/// ma ogni chiamata a <see cref="CreateOpenConnectionAsync"/> apre una
/// connessione nuova. Per questo motivo il servizio può essere
/// registrato come <c>Singleton</c> nel DI container.
/// </summary>
internal sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    // Nome logico della connection string usata in tutta l'app.
    // Centralizzato qui per evitare stringhe magiche disseminate.
    private const string ConnectionStringName = "Default";

    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        // Fallisce subito (fail-fast) se la connection string non è
        // configurata: meglio un errore esplicito all'avvio che un
        // NullReferenceException criptico alla prima query.
        _connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' non configurata. " +
                "Aggiungila in appsettings.json o in user-secrets.");
    }

    public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
