using Dapper;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Models;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Repositories;

/// <summary>
/// Implementazione Dapper di <see cref="IVerbaleConsultazioneRepository"/>.
/// Legge la vista <c>fatt.VerbaliConsultazione</c> (migration 073 di ICMVerbali),
/// che incapsula i join del dominio verbali e filtra già a "solo firmati non
/// cancellati" (<c>Stato &gt; 0 AND IsDeleted = 0</c>). Qui si aggiunge il solo
/// vincolo <c>ReportPath IS NOT NULL</c> (candidati all'export); l'esistenza
/// fisica del file è verificata a valle dal Manager.
/// </summary>
internal sealed class VerbaleConsultazioneRepository : IVerbaleConsultazioneRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public VerbaleConsultazioneRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    // DTO di riga: Data è DATE SQL → letta come DateTime (Dapper non mappa
    // automaticamente DATE→DateOnly in lettura), EsitoSeverita è tinyint NULL →
    // byte?. La conversione all'entità avviene in ToModel.
    private sealed class Row
    {
        public Guid      IdVerbale             { get; init; }
        public int?      Numero                { get; init; }
        public int?      Anno                  { get; init; }
        public DateTime  Data                  { get; init; }
        public Guid      IdAnagrafica          { get; init; }
        public string    RagioneSocialeCliente { get; init; } = string.Empty;
        public Guid      IdAttivita            { get; init; }
        public string    NumeroAttivita        { get; init; } = string.Empty;
        public string    DescrizioneAttivita   { get; init; } = string.Empty;
        public Guid      IdCantiere            { get; init; }
        public string    UbicazioneCantiere    { get; init; } = string.Empty;
        public string?   CseNominativo         { get; init; }
        public string?   ImpresaAppaltatrice   { get; init; }
        public string?   EsitoEtichetta        { get; init; }
        public byte?     EsitoSeverita         { get; init; }
        public int       NumeroPrescrizioni    { get; init; }
        public string?   ReportPath            { get; init; }
    }

    private static VerbaleConsultazione ToModel(Row r) => new()
    {
        IdVerbale             = r.IdVerbale,
        Numero                = r.Numero,
        Anno                  = r.Anno,
        Data                  = DateOnly.FromDateTime(r.Data),
        IdAnagrafica          = r.IdAnagrafica,
        RagioneSocialeCliente = r.RagioneSocialeCliente,
        IdAttivita            = r.IdAttivita,
        NumeroAttivita        = r.NumeroAttivita,
        DescrizioneAttivita   = r.DescrizioneAttivita,
        IdCantiere            = r.IdCantiere,
        UbicazioneCantiere    = r.UbicazioneCantiere,
        CseNominativo         = r.CseNominativo,
        ImpresaAppaltatrice   = r.ImpresaAppaltatrice,
        EsitoEtichetta        = r.EsitoEtichetta,
        EsitoSeverita         = r.EsitoSeverita is byte s ? (SeveritaEsito)s : null,
        NumeroPrescrizioni    = r.NumeroPrescrizioni,
        ReportPath            = r.ReportPath,
    };

    private const string SqlSelectEsportabili = """
        SELECT IdVerbale, Numero, Anno, Data,
               IdAnagrafica, RagioneSocialeCliente,
               IdAttivita, NumeroAttivita, DescrizioneAttivita,
               IdCantiere, UbicazioneCantiere,
               CseNominativo, ImpresaAppaltatrice,
               EsitoEtichetta, EsitoSeverita,
               NumeroPrescrizioni, ReportPath
        FROM fatt.VerbaliConsultazione
        WHERE ReportPath IS NOT NULL
        ORDER BY Data DESC, Numero DESC;
        """;

    public async Task<IReadOnlyList<VerbaleConsultazione>> GetEsportabiliAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = new CommandDefinition(SqlSelectEsportabili, cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<Row>(cmd);
        return rows.Select(ToModel).ToList();
    }
}
