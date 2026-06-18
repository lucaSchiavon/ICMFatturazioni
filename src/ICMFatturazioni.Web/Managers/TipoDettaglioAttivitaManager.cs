using ICMFatturazioni.Web.Auditing;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Repositories.Interfaces;
using Microsoft.Data.SqlClient;

namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Implementazione di <see cref="ITipoDettaglioAttivitaManager"/>:
/// normalizzazione, validazione unicità, audit, doppia difesa su DELETE.
/// </summary>
internal sealed class TipoDettaglioAttivitaManager : ITipoDettaglioAttivitaManager
{
    private const int SqlErrorDuplicateIndexKey   = 2601;
    private const int SqlErrorDuplicateConstraint = 2627;
    private const string EntityType = nameof(TipoDettaglioAttivita);

    private readonly ITipoDettaglioAttivitaRepository _repository;
    private readonly IAuditManager _audit;

    public TipoDettaglioAttivitaManager(ITipoDettaglioAttivitaRepository repository, IAuditManager audit)
    {
        _repository = repository;
        _audit = audit;
    }

    public Task<IReadOnlyList<TipoDettaglioAttivita>> ElencoAsync(CancellationToken cancellationToken = default)
        => _repository.GetAttiviAsync(cancellationToken);

    public Task<TipoDettaglioAttivita?> GetByIdAsync(Guid idTipoDettaglioAttivita, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(idTipoDettaglioAttivita, cancellationToken);

    public async Task<Guid> CreaAsync(TipoDettaglioAttivita tipo, CancellationToken cancellationToken = default)
    {
        var norm = Normalizza(tipo);
        ValidaCampi(norm);
        await ValidaUnicitaAsync(norm, escludiId: null, cancellationToken);

        norm.IdTipoDettaglioAttivita = Guid.CreateVersion7();
        try
        {
            await _repository.InsertAsync(norm, cancellationToken);
            await _audit.RegistraCreazioneAsync(EntityType, norm.IdTipoDettaglioAttivita,
                norm.Descrizione, AuditDettaglio.Snapshot(norm), cancellationToken);
            return norm.IdTipoDettaglioAttivita;
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            throw new TipoDettaglioAttivitaInvalidaException(
                TipoDettaglioAttivitaInvalidoMotivo.DescrizioneDuplicata,
                "Esiste già un tipo dettaglio attivo con questa descrizione.", ex);
        }
    }

    public async Task AggiornaAsync(TipoDettaglioAttivita tipo, CancellationToken cancellationToken = default)
    {
        var norm = Normalizza(tipo);
        ValidaCampi(norm);
        await ValidaUnicitaAsync(norm, escludiId: norm.IdTipoDettaglioAttivita, cancellationToken);

        var precedente = await _repository.GetByIdAsync(norm.IdTipoDettaglioAttivita, cancellationToken);
        try
        {
            await _repository.UpdateAsync(norm, cancellationToken);
            var dati = precedente is null
                ? AuditDettaglio.Snapshot(norm)
                : AuditDettaglio.Diff(precedente, norm);
            await _audit.RegistraModificaAsync(EntityType, norm.IdTipoDettaglioAttivita,
                norm.Descrizione, dati, cancellationToken);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            throw new TipoDettaglioAttivitaInvalidaException(
                TipoDettaglioAttivitaInvalidoMotivo.DescrizioneDuplicata,
                "Esiste già un tipo dettaglio attivo con questa descrizione.", ex);
        }
    }

    public async Task EliminaAsync(Guid idTipoDettaglioAttivita, CancellationToken cancellationToken = default)
    {
        if (await _repository.HasDipendenzeAsync(idTipoDettaglioAttivita, cancellationToken))
            throw new TipoDettaglioAttivitaConDipendenzeException(idTipoDettaglioAttivita);

        var tipo = await _repository.GetByIdAsync(idTipoDettaglioAttivita, cancellationToken);
        await _repository.DisattivaAsync(idTipoDettaglioAttivita, cancellationToken);
        var dati = tipo is null ? null : AuditDettaglio.Snapshot(tipo);
        await _audit.RegistraEliminazioneAsync(EntityType, idTipoDettaglioAttivita,
            tipo?.Descrizione, dati, cancellationToken);
    }

    public async Task<bool> EEliminabileAsync(Guid idTipoDettaglioAttivita, CancellationToken cancellationToken = default)
        => !await _repository.HasDipendenzeAsync(idTipoDettaglioAttivita, cancellationToken);

    private static TipoDettaglioAttivita Normalizza(TipoDettaglioAttivita t) => new()
    {
        IdTipoDettaglioAttivita = t.IdTipoDettaglioAttivita,
        Descrizione             = t.Descrizione?.Trim().ToUpperInvariant() ?? string.Empty,
        IsAttivo                = t.IsAttivo,
    };

    private static void ValidaCampi(TipoDettaglioAttivita t)
    {
        if (string.IsNullOrWhiteSpace(t.Descrizione))
            throw new TipoDettaglioAttivitaInvalidaException(
                TipoDettaglioAttivitaInvalidoMotivo.DescrizioneObbligatoria,
                "La descrizione del tipo dettaglio attività è obbligatoria.");
    }

    private async Task ValidaUnicitaAsync(TipoDettaglioAttivita t, Guid? escludiId, CancellationToken ct)
    {
        if (await _repository.ExistsDescrizioneAttivaAsync(t.Descrizione, escludiId, ct))
            throw new TipoDettaglioAttivitaInvalidaException(
                TipoDettaglioAttivitaInvalidoMotivo.DescrizioneDuplicata,
                $"Esiste già un tipo dettaglio attivo con descrizione \"{t.Descrizione}\".");
    }
}
