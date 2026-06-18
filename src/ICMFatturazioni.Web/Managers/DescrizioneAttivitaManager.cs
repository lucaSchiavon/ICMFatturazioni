using ICMFatturazioni.Web.Auditing;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Repositories.Interfaces;
using Microsoft.Data.SqlClient;

namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Implementazione di <see cref="IDescrizioneAttivitaManager"/>:
/// normalizzazione, validazione unicità, audit. Nessuna dipendenza da eliminare
/// (le descrizioni sono un catalogo di suggerimenti non vincolato via FK).
/// </summary>
internal sealed class DescrizioneAttivitaManager : IDescrizioneAttivitaManager
{
    private const string EntityType = nameof(DescrizioneAttivita);

    private readonly IDescrizioneAttivitaRepository _repository;
    private readonly IAuditManager _audit;

    public DescrizioneAttivitaManager(IDescrizioneAttivitaRepository repository, IAuditManager audit)
    {
        _repository = repository;
        _audit = audit;
    }

    public Task<IReadOnlyList<DescrizioneAttivita>> ElencoAsync(CancellationToken cancellationToken = default)
        => _repository.GetAttiviAsync(cancellationToken);

    public Task<DescrizioneAttivita?> GetByIdAsync(Guid idDescrizioneAttivita, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(idDescrizioneAttivita, cancellationToken);

    public async Task<Guid> CreaAsync(DescrizioneAttivita descrizione, CancellationToken cancellationToken = default)
    {
        var norm = Normalizza(descrizione);
        ValidaCampi(norm);
        await ValidaUnicitaAsync(norm, escludiId: null, cancellationToken);

        norm.IdDescrizioneAttivita = Guid.CreateVersion7();
        try
        {
            await _repository.InsertAsync(norm, cancellationToken);
            await _audit.RegistraCreazioneAsync(EntityType, norm.IdDescrizioneAttivita,
                norm.Descrizione, AuditDettaglio.Snapshot(norm), cancellationToken);
            return norm.IdDescrizioneAttivita;
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            throw new DescrizioneAttivitaInvalidaException(
                DescrizioneAttivitaInvalidoMotivo.DescrizioneDuplicata,
                "Esiste già una descrizione attiva con questo testo.", ex);
        }
    }

    public async Task AggiornaAsync(DescrizioneAttivita descrizione, CancellationToken cancellationToken = default)
    {
        var norm = Normalizza(descrizione);
        ValidaCampi(norm);
        await ValidaUnicitaAsync(norm, escludiId: norm.IdDescrizioneAttivita, cancellationToken);

        var precedente = await _repository.GetByIdAsync(norm.IdDescrizioneAttivita, cancellationToken);
        try
        {
            await _repository.UpdateAsync(norm, cancellationToken);
            var dati = precedente is null
                ? AuditDettaglio.Snapshot(norm)
                : AuditDettaglio.Diff(precedente, norm);
            await _audit.RegistraModificaAsync(EntityType, norm.IdDescrizioneAttivita, norm.Descrizione, dati, cancellationToken);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            throw new DescrizioneAttivitaInvalidaException(
                DescrizioneAttivitaInvalidoMotivo.DescrizioneDuplicata,
                "Esiste già una descrizione attiva con questo testo.", ex);
        }
    }

    public async Task EliminaAsync(Guid idDescrizioneAttivita, CancellationToken cancellationToken = default)
    {
        var desc = await _repository.GetByIdAsync(idDescrizioneAttivita, cancellationToken);
        await _repository.DisattivaAsync(idDescrizioneAttivita, cancellationToken);
        var dati = desc is null ? null : AuditDettaglio.Snapshot(desc);
        await _audit.RegistraEliminazioneAsync(EntityType, idDescrizioneAttivita, desc?.Descrizione, dati, cancellationToken);
    }

    private static DescrizioneAttivita Normalizza(DescrizioneAttivita d) => new()
    {
        IdDescrizioneAttivita = d.IdDescrizioneAttivita,
        Descrizione           = d.Descrizione?.Trim() ?? string.Empty,
        Ordine                = d.Ordine < 0 ? 0 : d.Ordine,
        IsAttivo              = d.IsAttivo,
    };

    private static void ValidaCampi(DescrizioneAttivita d)
    {
        if (string.IsNullOrWhiteSpace(d.Descrizione))
            throw new DescrizioneAttivitaInvalidaException(
                DescrizioneAttivitaInvalidoMotivo.DescrizioneObbligatoria,
                "Il testo della descrizione è obbligatorio.");
    }

    private async Task ValidaUnicitaAsync(DescrizioneAttivita d, Guid? escludiId, CancellationToken ct)
    {
        if (await _repository.ExistsDescrizioneAttivaAsync(d.Descrizione, escludiId, ct))
            throw new DescrizioneAttivitaInvalidaException(
                DescrizioneAttivitaInvalidoMotivo.DescrizioneDuplicata,
                $"Esiste già una descrizione attiva con testo \"{d.Descrizione}\".");
    }
}
