using ICMFatturazioni.Web.Auditing;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Repositories.Interfaces;
using Microsoft.Data.SqlClient;

namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Implementazione di <see cref="ITipoAttivitaManager"/>:
///   1) normalizzazione (trim; descrizione in maiuscolo per coerenza con il legacy),
///   2) validazione (descrizione obbligatoria, unicità tra gli attivi),
///   3) traduzione SqlException → <see cref="TipoAttivitaInvalidaException"/>,
///   4) doppia difesa su DELETE (HasDipendenze pre-check + soft-delete),
///   5) audit di ogni scrittura (Regola 7).
/// </summary>
internal sealed class TipoAttivitaManager : ITipoAttivitaManager
{
    private const int SqlErrorDuplicateIndexKey   = 2601;
    private const int SqlErrorDuplicateConstraint = 2627;
    private const string EntityType = nameof(TipoAttivita);

    private readonly ITipoAttivitaRepository _repository;
    private readonly IAuditManager _audit;

    public TipoAttivitaManager(ITipoAttivitaRepository repository, IAuditManager audit)
    {
        _repository = repository;
        _audit = audit;
    }

    public Task<IReadOnlyList<TipoAttivita>> ElencoAsync(CancellationToken cancellationToken = default)
        => _repository.GetAttiviAsync(cancellationToken);

    public Task<TipoAttivita?> GetByIdAsync(Guid idTipoAttivita, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(idTipoAttivita, cancellationToken);

    public async Task<Guid> CreaAsync(TipoAttivita tipo, CancellationToken cancellationToken = default)
    {
        var norm = Normalizza(tipo);
        ValidaCampi(norm);
        await ValidaUnicitaAsync(norm, escludiId: null, cancellationToken);

        norm.IdTipoAttivita = Guid.CreateVersion7();
        try
        {
            await _repository.InsertAsync(norm, cancellationToken);
            await _audit.RegistraCreazioneAsync(EntityType, norm.IdTipoAttivita, norm.Descrizione,
                AuditDettaglio.Snapshot(norm), cancellationToken);
            return norm.IdTipoAttivita;
        }
        catch (SqlException ex) when (IsViolazioneVincolo(ex))
        {
            throw TraduciViolazione(ex);
        }
    }

    public async Task AggiornaAsync(TipoAttivita tipo, CancellationToken cancellationToken = default)
    {
        var norm = Normalizza(tipo);
        ValidaCampi(norm);
        await ValidaUnicitaAsync(norm, escludiId: norm.IdTipoAttivita, cancellationToken);

        var precedente = await _repository.GetByIdAsync(norm.IdTipoAttivita, cancellationToken);
        try
        {
            await _repository.UpdateAsync(norm, cancellationToken);
            var dati = precedente is null
                ? AuditDettaglio.Snapshot(norm)
                : AuditDettaglio.Diff(precedente, norm);
            await _audit.RegistraModificaAsync(EntityType, norm.IdTipoAttivita, norm.Descrizione, dati, cancellationToken);
        }
        catch (SqlException ex) when (IsViolazioneVincolo(ex))
        {
            throw TraduciViolazione(ex);
        }
    }

    public async Task EliminaAsync(Guid idTipoAttivita, CancellationToken cancellationToken = default)
    {
        if (await _repository.HasDipendenzeAsync(idTipoAttivita, cancellationToken))
            throw new TipoAttivitaConDipendenzeException(idTipoAttivita);

        var tipo = await _repository.GetByIdAsync(idTipoAttivita, cancellationToken);
        await _repository.DisattivaAsync(idTipoAttivita, cancellationToken);
        var dati = tipo is null ? null : AuditDettaglio.Snapshot(tipo);
        await _audit.RegistraEliminazioneAsync(EntityType, idTipoAttivita, tipo?.Descrizione, dati, cancellationToken);
    }

    public async Task<bool> EEliminabileAsync(Guid idTipoAttivita, CancellationToken cancellationToken = default)
        => !await _repository.HasDipendenzeAsync(idTipoAttivita, cancellationToken);

    // -------------------------------------------------------------------------

    private static TipoAttivita Normalizza(TipoAttivita t) => new()
    {
        IdTipoAttivita = t.IdTipoAttivita,
        // Maiuscolo per coerenza con i valori legacy (CONSULENZE, PROGETTAZIONI, ALTRO).
        Descrizione    = t.Descrizione?.Trim().ToUpperInvariant() ?? string.Empty,
        GestisciCome   = t.GestisciCome,
        StudiSettore   = t.StudiSettore,
        IsAttivo       = t.IsAttivo,
    };

    private static void ValidaCampi(TipoAttivita t)
    {
        if (string.IsNullOrWhiteSpace(t.Descrizione))
            throw new TipoAttivitaInvalidaException(
                TipoAttivitaInvalidoMotivo.DescrizioneObbligatoria,
                "La descrizione del tipo attività è obbligatoria.");
    }

    private async Task ValidaUnicitaAsync(TipoAttivita t, Guid? escludiId, CancellationToken ct)
    {
        if (await _repository.ExistsDescrizioneAttivaAsync(t.Descrizione, escludiId, ct))
            throw new TipoAttivitaInvalidaException(
                TipoAttivitaInvalidoMotivo.DescrizioneDuplicata,
                $"Esiste già un tipo attività attivo con descrizione \"{t.Descrizione}\".");
    }

    private static bool IsViolazioneVincolo(SqlException ex)
        => ex.Number is SqlErrorDuplicateIndexKey or SqlErrorDuplicateConstraint;

    private static TipoAttivitaInvalidaException TraduciViolazione(SqlException ex)
        => new(TipoAttivitaInvalidoMotivo.DescrizioneDuplicata,
               "Esiste già un tipo attività attivo con questa descrizione.", ex);
}
