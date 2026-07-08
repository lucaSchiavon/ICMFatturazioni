using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;
using ICMFatturazioni.Web.Models;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Implementazione in-memory di <see cref="IFattureRepository"/> per i test del
/// manager. Riproduce le due guardie degli indici univoci filtrati (un avviso ha al
/// più una fattura attiva; numero unico per anno) lanciando
/// <see cref="FatturaInvalidaException"/> con il motivo corrispondente.
/// </summary>
internal sealed class FakeFattureRepository : IFattureRepository
{
    private readonly Dictionary<Guid, Fattura> _fatture = new();

    public Task<Fattura?> GetByIdAsync(Guid idFattura, CancellationToken ct = default)
        => Task.FromResult(_fatture.TryGetValue(idFattura, out var f) ? f : null);

    public Task<Fattura?> GetAttivaByAvvisoAsync(Guid idAvviso, CancellationToken ct = default)
        => Task.FromResult(_fatture.Values.FirstOrDefault(f => f.IdAvviso == idAvviso && f.IsAttivo));

    public Task<int> GetMaxNumeroAnnoAsync(int anno, CancellationToken ct = default)
    {
        var attive = _fatture.Values.Where(f => f.IsAttivo && f.Anno == anno).ToList();
        return Task.FromResult(attive.Count == 0 ? 0 : attive.Max(f => f.NumeroFattura));
    }

    public Task<IReadOnlyList<FatturaNumeroData>> GetNumeriDateAnnoAsync(int anno, CancellationToken ct = default)
    {
        var coppie = _fatture.Values
            .Where(f => f.IsAttivo && f.Anno == anno)
            .Select(f => new FatturaNumeroData(f.NumeroFattura, f.DataFattura))
            .ToList();
        return Task.FromResult<IReadOnlyList<FatturaNumeroData>>(coppie);
    }

    public Task CreateAsync(Fattura fattura, CancellationToken ct = default)
    {
        // Guardia UQ_Fatture_IdAvviso_Attiva.
        if (_fatture.Values.Any(f => f.IsAttivo && f.IdAvviso == fattura.IdAvviso))
            throw new FatturaInvalidaException(
                FatturaMotivoInvalido.AvvisoGiaFatturato, "Avviso già fatturato.");

        // Guardia UQ_Fatture_Anno_Numero_Attiva.
        if (_fatture.Values.Any(f => f.IsAttivo && f.Anno == fattura.Anno && f.NumeroFattura == fattura.NumeroFattura))
            throw new FatturaInvalidaException(
                FatturaMotivoInvalido.NumeroDuplicato, "Numero già usato nell'anno.");

        _fatture[fattura.IdFattura] = fattura;
        return Task.CompletedTask;
    }

    public Task AnnullaAsync(Guid idFattura, CancellationToken ct = default)
    {
        if (_fatture.TryGetValue(idFattura, out var f))
            _fatture[idFattura] = CloneInattiva(f);
        return Task.CompletedTask;
    }

    // ── Supporto per le letture della maschera "Stampe fatture" ──────────────
    // Il fake non ha i join reali: i test mappano avviso→attività/cliente qui, così
    // le tre letture possono derivare i read-model dalle fatture in memoria.
    public readonly Dictionary<Guid, Guid> AvvisoToAttivita   = new();
    public readonly Dictionary<Guid, Guid> AvvisoToAnagrafica = new();

    public Task<IReadOnlyList<FatturaEmessa>> GetEmesseByAttivitaAsync(Guid idAttivita, CancellationToken ct = default)
    {
        var righe = _fatture.Values
            .Where(f => f.IsAttivo
                        && AvvisoToAttivita.TryGetValue(f.IdAvviso, out var idAtt)
                        && idAtt == idAttivita)
            .OrderByDescending(f => f.Anno).ThenByDescending(f => f.NumeroFattura)
            .Select(f => new FatturaEmessa(
                f.IdFattura, f.IdAvviso, f.NumeroFattura, f.Anno, f.DataFattura,
                "Cliente", "Tipo", "0", "Attività", f.CreatoXML, f.EsitoXML))
            .ToList();
        return Task.FromResult<IReadOnlyList<FatturaEmessa>>(righe);
    }

    public Task<IReadOnlyList<int>> GetAnniConFattureAsync(CancellationToken ct = default)
    {
        var anni = _fatture.Values.Where(f => f.IsAttivo)
            .Select(f => f.Anno).Distinct().OrderByDescending(a => a).ToList();
        return Task.FromResult<IReadOnlyList<int>>(anni);
    }

    public Task<IReadOnlyList<AttivitaFatturabile>> GetAttivitaConFattureAsync(CancellationToken ct = default)
    {
        var coppie = _fatture.Values.Where(f => f.IsAttivo)
            .Where(f => AvvisoToAttivita.ContainsKey(f.IdAvviso) && AvvisoToAnagrafica.ContainsKey(f.IdAvviso))
            .Select(f => new AttivitaFatturabile(AvvisoToAnagrafica[f.IdAvviso], AvvisoToAttivita[f.IdAvviso]))
            .Distinct().ToList();
        return Task.FromResult<IReadOnlyList<AttivitaFatturabile>>(coppie);
    }

    // ── Fase D1 — maschera "Creazione-Gestione XML Documenti" ─────────────────

    // Sequence in-memory del progressivo invio.
    private long _seqProgressivo;

    // Tipo anagrafica per avviso (default Privato), per il read-model della griglia.
    public readonly Dictionary<Guid, TipoAnagrafica> AvvisoToTipo = new();

    public Task<long> GetNextProgressivoInvioAsync(CancellationToken ct = default)
        => Task.FromResult(++_seqProgressivo);

    public Task SetXmlCreatoAsync(Guid idFattura, string progressivoInvio, string nomeFileXml, DateTime creatoUtc, CancellationToken ct = default)
    {
        if (_fatture.TryGetValue(idFattura, out var f))
            _fatture[idFattura] = Clone(f, creatoXml: true, progressivo: progressivoInvio,
                nomeFile: nomeFileXml, creatoUtc: creatoUtc);
        return Task.CompletedTask;
    }

    public Task ConfermaEsitoAsync(Guid idFattura, DateTime esitoUtc, CancellationToken ct = default)
    {
        if (_fatture.TryGetValue(idFattura, out var f))
            _fatture[idFattura] = Clone(f, esitoXml: 1, esitoUtc: esitoUtc);
        return Task.CompletedTask;
    }

    public Task TogliEsitoAsync(Guid idFattura, CancellationToken ct = default)
    {
        if (_fatture.TryGetValue(idFattura, out var f))
            _fatture[idFattura] = Clone(f, esitoXml: 0, esitoUtc: null, azzeraEsitoUtc: true);
        return Task.CompletedTask;
    }

    public Task ResetXmlAsync(Guid idFattura, CancellationToken ct = default)
    {
        // Replica il sentinel `AND EsitoXML = 0` della query reale: non tocca le
        // fatture con esito OK. Azzera esplicitamente i metadati XML.
        if (_fatture.TryGetValue(idFattura, out var f) && f.IsAttivo && f.EsitoXML == 0)
        {
            _fatture[idFattura] = new Fattura
            {
                IdFattura           = f.IdFattura,
                IdAvviso            = f.IdAvviso,
                NumeroFattura       = f.NumeroFattura,
                Anno                = f.Anno,
                DataFattura         = f.DataFattura,
                CreatoXML           = false,
                EsitoXML            = f.EsitoXML,
                ProgressivoInvio    = null,
                NomeFileXml         = null,
                DataCreazioneXmlUtc = null,
                DataEsitoXmlUtc     = f.DataEsitoXmlUtc,
                Cig                 = f.Cig,
                Cup                 = f.Cup,
                IsAttivo            = f.IsAttivo,
            };
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DocumentoXmlRiga>> GetPerXmlAsync(FiltroDocumentiXml filtro, CancellationToken ct = default)
    {
        var righe = _fatture.Values
            .Where(f => f.IsAttivo
                        && f.DataFattura >= filtro.DataDa
                        && f.DataFattura <= filtro.DataA)
            .Where(f => filtro.IdAnagrafica is null
                        || (AvvisoToAnagrafica.TryGetValue(f.IdAvviso, out var idAna) && idAna == filtro.IdAnagrafica))
            .Where(f => filtro.Creazione switch
            {
                StatoCreazioneXml.DaCreare => !f.CreatoXML,
                StatoCreazioneXml.Creato   => f.CreatoXML,
                _                          => true,
            })
            .Where(f => filtro.Esito switch
            {
                StatoEsitoXml.Attesa => f.EsitoXML == 0,
                StatoEsitoXml.Ok     => f.EsitoXML == 1,
                _                    => true,
            })
            .OrderByDescending(f => f.DataFattura).ThenByDescending(f => f.NumeroFattura)
            .Select(f => new DocumentoXmlRiga(
                f.IdFattura, f.NumeroFattura, f.Anno, f.DataFattura,
                AvvisoToTipo.TryGetValue(f.IdAvviso, out var tp) ? tp : TipoAnagrafica.Privato,
                "Cliente", "Tipo", "0", "Attività",
                f.CreatoXML, f.EsitoXML, f.ProgressivoInvio, f.NomeFileXml))
            .ToList();
        return Task.FromResult<IReadOnlyList<DocumentoXmlRiga>>(righe);
    }

    private static Fattura CloneInattiva(Fattura src) => Clone(src, isAttivo: false);

    // Clone con override selettivi (l'entità è a init-only): base per soft-delete e
    // per le transizioni di stato XML.
    private static Fattura Clone(
        Fattura src,
        bool?     isAttivo   = null,
        bool?     creatoXml  = null,
        int?      esitoXml   = null,
        string?   progressivo = null,
        string?   nomeFile   = null,
        DateTime? creatoUtc  = null,
        DateTime? esitoUtc   = null,
        bool      azzeraEsitoUtc = false) => new()
    {
        IdFattura           = src.IdFattura,
        IdAvviso            = src.IdAvviso,
        NumeroFattura       = src.NumeroFattura,
        Anno                = src.Anno,
        DataFattura         = src.DataFattura,
        CreatoXML           = creatoXml ?? src.CreatoXML,
        EsitoXML            = esitoXml  ?? src.EsitoXML,
        ProgressivoInvio    = progressivo ?? src.ProgressivoInvio,
        NomeFileXml         = nomeFile ?? src.NomeFileXml,
        DataCreazioneXmlUtc = creatoUtc ?? src.DataCreazioneXmlUtc,
        DataEsitoXmlUtc     = azzeraEsitoUtc ? null : (esitoUtc ?? src.DataEsitoXmlUtc),
        Cig                 = src.Cig,
        Cup                 = src.Cup,
        IsAttivo            = isAttivo  ?? src.IsAttivo,
    };
}
