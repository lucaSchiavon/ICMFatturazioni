using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Repositories;

/// <summary>Implementazione Dapper di <see cref="IAziendaRepository"/>.</summary>
internal sealed class AziendaRepository : IAziendaRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public AziendaRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    // La colonna legacy "Azienda" (nome breve) è aliasata su NomeBreve.
    // TOP(1) IsAttivo: sistema mono-studio → "l'azienda corrente".
    private const string SqlSelectCorrente = """
        SELECT TOP (1)
            IdAzienda, Azienda AS NomeBreve, RagioneSociale, PIVA, CodiceFiscale,
            IndirizzoVia, IndirizzoCivico, IndirizzoComune, IndirizzoProvincia, IndirizzoCAP, IndirizzoPaese,
            Telefono, Telefax, Email, PEC,
            REA, REAFe, CCIAA, CCIAAFe, CapitaleSociale, CapitaleSocialeFe,
            RegimeFiscale, StatoLiquidazione, SocioUnico, Identificativo, IsAttivo
        FROM fatt.Azienda
        WHERE IsAttivo = 1
        ORDER BY RagioneSociale;
        """;

    public async Task<Azienda?> GetAziendaAsync(CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(SqlSelectCorrente, cancellationToken: ct);
        return await conn.QuerySingleOrDefaultAsync<Azienda>(cmd);
    }
}
