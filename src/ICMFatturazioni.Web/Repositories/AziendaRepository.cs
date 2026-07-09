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
            RegimeFiscale, StatoLiquidazione, SocioUnico, Identificativo,
            ApplicaCassaPrevidenziale, TipoCassaFe,
            SoggettoARitenuta, TipoRitenutaFe, CausalePagamentoRitenutaFe,
            IsAttivo
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

    // La colonna legacy "Azienda" (nome breve) è alimentata dal parametro @NomeBreve.
    private const string SqlInsert = """
        INSERT INTO fatt.Azienda
            (IdAzienda, Azienda, RagioneSociale, PIVA, CodiceFiscale,
             IndirizzoVia, IndirizzoCivico, IndirizzoComune, IndirizzoProvincia, IndirizzoCAP, IndirizzoPaese,
             Telefono, Telefax, Email, PEC,
             REA, REAFe, CCIAA, CCIAAFe, CapitaleSociale, CapitaleSocialeFe,
             RegimeFiscale, StatoLiquidazione, SocioUnico, Identificativo,
             ApplicaCassaPrevidenziale, TipoCassaFe,
             SoggettoARitenuta, TipoRitenutaFe, CausalePagamentoRitenutaFe,
             IsAttivo)
        VALUES
            (@IdAzienda, @NomeBreve, @RagioneSociale, @PIVA, @CodiceFiscale,
             @IndirizzoVia, @IndirizzoCivico, @IndirizzoComune, @IndirizzoProvincia, @IndirizzoCAP, @IndirizzoPaese,
             @Telefono, @Telefax, @Email, @PEC,
             @REA, @REAFe, @CCIAA, @CCIAAFe, @CapitaleSociale, @CapitaleSocialeFe,
             @RegimeFiscale, @StatoLiquidazione, @SocioUnico, @Identificativo,
             @ApplicaCassaPrevidenziale, @TipoCassaFe,
             @SoggettoARitenuta, @TipoRitenutaFe, @CausalePagamentoRitenutaFe,
             @IsAttivo);
        """;

    public async Task InsertAsync(Azienda azienda, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(SqlInsert, azienda, cancellationToken: ct);
        await conn.ExecuteAsync(cmd);
    }

    private const string SqlUpdate = """
        UPDATE fatt.Azienda SET
            Azienda                    = @NomeBreve,
            RagioneSociale             = @RagioneSociale,
            PIVA                       = @PIVA,
            CodiceFiscale              = @CodiceFiscale,
            IndirizzoVia               = @IndirizzoVia,
            IndirizzoCivico            = @IndirizzoCivico,
            IndirizzoComune            = @IndirizzoComune,
            IndirizzoProvincia         = @IndirizzoProvincia,
            IndirizzoCAP               = @IndirizzoCAP,
            IndirizzoPaese             = @IndirizzoPaese,
            Telefono                   = @Telefono,
            Telefax                    = @Telefax,
            Email                      = @Email,
            PEC                        = @PEC,
            REA                        = @REA,
            REAFe                      = @REAFe,
            CCIAA                      = @CCIAA,
            CCIAAFe                    = @CCIAAFe,
            CapitaleSociale            = @CapitaleSociale,
            CapitaleSocialeFe          = @CapitaleSocialeFe,
            RegimeFiscale              = @RegimeFiscale,
            StatoLiquidazione          = @StatoLiquidazione,
            SocioUnico                 = @SocioUnico,
            Identificativo             = @Identificativo,
            ApplicaCassaPrevidenziale  = @ApplicaCassaPrevidenziale,
            TipoCassaFe                = @TipoCassaFe,
            SoggettoARitenuta          = @SoggettoARitenuta,
            TipoRitenutaFe             = @TipoRitenutaFe,
            CausalePagamentoRitenutaFe = @CausalePagamentoRitenutaFe
        WHERE IdAzienda = @IdAzienda;
        """;

    public async Task UpdateAsync(Azienda azienda, CancellationToken ct = default)
    {
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var cmd = new CommandDefinition(SqlUpdate, azienda, cancellationToken: ct);
        await conn.ExecuteAsync(cmd);
    }
}
