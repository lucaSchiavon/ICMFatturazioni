using ICMFatturazioni.Web.Auditing;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Repositories.Interfaces;
using Microsoft.Data.SqlClient;

namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Implementazione di <see cref="ITipoAttivitaConsulenteManager"/>:
///   1) normalizzazione (trim; descrizione in maiuscolo, come il catalogo gemello
///      dei tipi attività studio),
///   2) validazione (descrizione obbligatoria, unicità tra gli attivi),
///   3) traduzione SqlException → <see cref="TipoAttivitaConsulenteInvalidoException"/>,
///   4) doppia difesa su DELETE (HasDipendenze pre-check + soft-delete),
///   5) audit di ogni scrittura (Regola 7).
/// </summary>
internal sealed class TipoAttivitaConsulenteManager : ITipoAttivitaConsulenteManager
{
    private const int SqlErrorDuplicateIndexKey   = 2601;
    private const int SqlErrorDuplicateConstraint = 2627;
    private const string EntityType = nameof(TipoAttivitaConsulente);

    private readonly ITipoAttivitaConsulenteRepository _repository;
    private readonly IAuditManager _audit;

    public TipoAttivitaConsulenteManager(ITipoAttivitaConsulenteRepository repository, IAuditManager audit)
    {
        _repository = repository;
        _audit = audit;
    }

    public Task<IReadOnlyList<TipoAttivitaConsulente>> ElencoAsync(CancellationToken cancellationToken = default)
        => _repository.GetAttiviAsync(cancellationToken);

    public Task<TipoAttivitaConsulente?> GetByIdAsync(Guid idTipoAttivitaConsulente, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(idTipoAttivitaConsulente, cancellationToken);

    public async Task<Guid> CreaAsync(TipoAttivitaConsulente tipo, CancellationToken cancellationToken = default)
    {
        var norm = Normalizza(tipo);
        ValidaCampi(norm);
        await ValidaUnicitaAsync(norm, escludiId: null, cancellationToken);

        norm.IdTipoAttivitaConsulente = Guid.CreateVersion7();
        try
        {
            await _repository.InsertAsync(norm, cancellationToken);
            await _audit.RegistraCreazioneAsync(EntityType, norm.IdTipoAttivitaConsulente, norm.Descrizione,
                AuditDettaglio.Snapshot(norm), cancellationToken);
            return norm.IdTipoAttivitaConsulente;
        }
        catch (SqlException ex) when (IsViolazioneVincolo(ex))
        {
            throw TraduciViolazione(ex);
        }
    }

    public async Task AggiornaAsync(TipoAttivitaConsulente tipo, CancellationToken cancellationToken = default)
    {
        var norm = Normalizza(tipo);
        ValidaCampi(norm);
        await ValidaUnicitaAsync(norm, escludiId: norm.IdTipoAttivitaConsulente, cancellationToken);

        var precedente = await _repository.GetByIdAsync(norm.IdTipoAttivitaConsulente, cancellationToken);
        try
        {
            await _repository.UpdateAsync(norm, cancellationToken);
            var dati = precedente is null
                ? AuditDettaglio.Snapshot(norm)
                : AuditDettaglio.Diff(precedente, norm);
            await _audit.RegistraModificaAsync(EntityType, norm.IdTipoAttivitaConsulente, norm.Descrizione, dati, cancellationToken);
        }
        catch (SqlException ex) when (IsViolazioneVincolo(ex))
        {
            throw TraduciViolazione(ex);
        }
    }

    public async Task EliminaAsync(Guid idTipoAttivitaConsulente, CancellationToken cancellationToken = default)
    {
        if (await _repository.HasDipendenzeAsync(idTipoAttivitaConsulente, cancellationToken))
            throw new TipoAttivitaConsulenteConDipendenzeException(idTipoAttivitaConsulente);

        var tipo = await _repository.GetByIdAsync(idTipoAttivitaConsulente, cancellationToken);
        await _repository.DisattivaAsync(idTipoAttivitaConsulente, cancellationToken);
        var dati = tipo is null ? null : AuditDettaglio.Snapshot(tipo);
        await _audit.RegistraEliminazioneAsync(EntityType, idTipoAttivitaConsulente, tipo?.Descrizione, dati, cancellationToken);
    }

    public async Task<bool> EEliminabileAsync(Guid idTipoAttivitaConsulente, CancellationToken cancellationToken = default)
        => !await _repository.HasDipendenzeAsync(idTipoAttivitaConsulente, cancellationToken);

    // -------------------------------------------------------------------------

    private static TipoAttivitaConsulente Normalizza(TipoAttivitaConsulente t) => new()
    {
        IdTipoAttivitaConsulente = t.IdTipoAttivitaConsulente,
        // Maiuscolo per coerenza con il catalogo gemello dei tipi attività studio.
        Descrizione              = t.Descrizione?.Trim().ToUpperInvariant() ?? string.Empty,
        IsAttivo                 = t.IsAttivo,
    };

    private static void ValidaCampi(TipoAttivitaConsulente t)
    {
        if (string.IsNullOrWhiteSpace(t.Descrizione))
            throw new TipoAttivitaConsulenteInvalidoException(
                TipoAttivitaConsulenteInvalidoMotivo.DescrizioneObbligatoria,
                "La descrizione del tipo attività consulente è obbligatoria.");
    }

    private async Task ValidaUnicitaAsync(TipoAttivitaConsulente t, Guid? escludiId, CancellationToken ct)
    {
        if (await _repository.ExistsDescrizioneAttivaAsync(t.Descrizione, escludiId, ct))
            throw new TipoAttivitaConsulenteInvalidoException(
                TipoAttivitaConsulenteInvalidoMotivo.DescrizioneDuplicata,
                $"Esiste già un tipo attività consulente attivo con descrizione \"{t.Descrizione}\".");
    }

    private static bool IsViolazioneVincolo(SqlException ex)
        => ex.Number is SqlErrorDuplicateIndexKey or SqlErrorDuplicateConstraint;

    private static TipoAttivitaConsulenteInvalidoException TraduciViolazione(SqlException ex)
        => new(TipoAttivitaConsulenteInvalidoMotivo.DescrizioneDuplicata,
               "Esiste già un tipo attività consulente attivo con questa descrizione.", ex);
}
