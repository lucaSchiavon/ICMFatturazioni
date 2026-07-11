using ICMFatturazioni.Web.Auditing;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Repositories.Interfaces;
using Microsoft.Data.SqlClient;

namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Implementazione di <see cref="IConsulenteManager"/>:
///   1) normalizzazione (trim; il nome del consulente resta com'è digitato,
///      diversamente dai cataloghi che vanno in maiuscolo),
///   2) validazione (denominazione obbligatoria, unicità tra gli attivi),
///   3) traduzione SqlException → <see cref="ConsulenteInvalidoException"/>,
///   4) doppia difesa su DELETE (HasDipendenze pre-check + soft-delete),
///   5) audit di ogni scrittura (Regola 7).
/// </summary>
internal sealed class ConsulenteManager : IConsulenteManager
{
    private const int SqlErrorDuplicateIndexKey   = 2601;
    private const int SqlErrorDuplicateConstraint = 2627;
    private const string EntityType = nameof(Consulente);

    private readonly IConsulenteRepository _repository;
    private readonly IAuditManager _audit;

    public ConsulenteManager(IConsulenteRepository repository, IAuditManager audit)
    {
        _repository = repository;
        _audit = audit;
    }

    public Task<IReadOnlyList<Consulente>> ElencoAsync(CancellationToken cancellationToken = default)
        => _repository.GetAttiviAsync(cancellationToken);

    public Task<Consulente?> GetByIdAsync(Guid idConsulente, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(idConsulente, cancellationToken);

    public async Task<Guid> CreaAsync(Consulente consulente, CancellationToken cancellationToken = default)
    {
        var norm = Normalizza(consulente);
        ValidaCampi(norm);
        await ValidaUnicitaAsync(norm, escludiId: null, cancellationToken);

        norm.IdConsulente = Guid.CreateVersion7();
        try
        {
            await _repository.InsertAsync(norm, cancellationToken);
            await _audit.RegistraCreazioneAsync(EntityType, norm.IdConsulente, norm.Descrizione,
                AuditDettaglio.Snapshot(norm), cancellationToken);
            return norm.IdConsulente;
        }
        catch (SqlException ex) when (IsViolazioneVincolo(ex))
        {
            throw TraduciViolazione(ex);
        }
    }

    public async Task AggiornaAsync(Consulente consulente, CancellationToken cancellationToken = default)
    {
        var norm = Normalizza(consulente);
        ValidaCampi(norm);
        await ValidaUnicitaAsync(norm, escludiId: norm.IdConsulente, cancellationToken);

        var precedente = await _repository.GetByIdAsync(norm.IdConsulente, cancellationToken);
        try
        {
            await _repository.UpdateAsync(norm, cancellationToken);
            var dati = precedente is null
                ? AuditDettaglio.Snapshot(norm)
                : AuditDettaglio.Diff(precedente, norm);
            await _audit.RegistraModificaAsync(EntityType, norm.IdConsulente, norm.Descrizione, dati, cancellationToken);
        }
        catch (SqlException ex) when (IsViolazioneVincolo(ex))
        {
            throw TraduciViolazione(ex);
        }
    }

    public async Task EliminaAsync(Guid idConsulente, CancellationToken cancellationToken = default)
    {
        if (await _repository.HasDipendenzeAsync(idConsulente, cancellationToken))
            throw new ConsulenteConDipendenzeException(idConsulente);

        var consulente = await _repository.GetByIdAsync(idConsulente, cancellationToken);
        await _repository.DisattivaAsync(idConsulente, cancellationToken);
        var dati = consulente is null ? null : AuditDettaglio.Snapshot(consulente);
        await _audit.RegistraEliminazioneAsync(EntityType, idConsulente, consulente?.Descrizione, dati, cancellationToken);
    }

    public async Task<bool> EEliminabileAsync(Guid idConsulente, CancellationToken cancellationToken = default)
        => !await _repository.HasDipendenzeAsync(idConsulente, cancellationToken);

    // -------------------------------------------------------------------------

    private static Consulente Normalizza(Consulente c) => new()
    {
        IdConsulente = c.IdConsulente,
        Descrizione  = c.Descrizione?.Trim() ?? string.Empty,
        IsAttivo     = c.IsAttivo,
    };

    private static void ValidaCampi(Consulente c)
    {
        if (string.IsNullOrWhiteSpace(c.Descrizione))
            throw new ConsulenteInvalidoException(
                ConsulenteInvalidoMotivo.DescrizioneObbligatoria,
                "Il nome del consulente è obbligatorio.");
    }

    private async Task ValidaUnicitaAsync(Consulente c, Guid? escludiId, CancellationToken ct)
    {
        if (await _repository.ExistsDescrizioneAttivaAsync(c.Descrizione, escludiId, ct))
            throw new ConsulenteInvalidoException(
                ConsulenteInvalidoMotivo.DescrizioneDuplicata,
                $"Esiste già un consulente attivo con nome \"{c.Descrizione}\".");
    }

    private static bool IsViolazioneVincolo(SqlException ex)
        => ex.Number is SqlErrorDuplicateIndexKey or SqlErrorDuplicateConstraint;

    private static ConsulenteInvalidoException TraduciViolazione(SqlException ex)
        => new(ConsulenteInvalidoMotivo.DescrizioneDuplicata,
               "Esiste già un consulente attivo con questo nome.", ex);
}
