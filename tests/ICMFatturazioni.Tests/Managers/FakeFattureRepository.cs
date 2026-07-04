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
                "Cliente", "Tipo", "0", "Attività"))
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

    private static Fattura CloneInattiva(Fattura src) => new()
    {
        IdFattura     = src.IdFattura,
        IdAvviso      = src.IdAvviso,
        NumeroFattura = src.NumeroFattura,
        Anno          = src.Anno,
        DataFattura   = src.DataFattura,
        CreatoXML     = src.CreatoXML,
        EsitoXML      = src.EsitoXML,
        IsAttivo      = false,
    };
}
