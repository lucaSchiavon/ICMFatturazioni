using ICMFatturazioni.Web.Auditing;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Repositories.Interfaces;
using Microsoft.Data.SqlClient;

namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Implementazione di <see cref="IAnagraficaManager"/>.
/// Concentra qui le regole di business non FK:
///   1) validazione dei campi obbligatori (RagioneSociale),
///   2) traduzione delle SqlException FK in
///      <see cref="AnagraficaInvalidaException"/> con motivo specifico,
///   3) pattern "doppia difesa" sulla DELETE.
/// </summary>
internal sealed class AnagraficaManager : IAnagraficaManager
{
    // Codici di errore SQL Server rilevanti per la traduzione delle eccezioni.
    // 547 = constraint violation (FK, CHECK, ecc.). Su Insert/Update di
    // Anagrafica le sole FK sono FK_Anagrafica_Paesi e FK_Anagrafica_Province.
    private const int SqlErrorConstraintViolation = 547;

    // EntityType usato nelle righe di audit per questa entità.
    private const string EntityType = nameof(Anagrafica);

    private readonly IAnagraficaRepository _repository;
    private readonly IAuditManager _audit;

    public AnagraficaManager(IAnagraficaRepository repository, IAuditManager audit)
    {
        _repository = repository;
        _audit = audit;
    }

    // ---------------------------------------------------------------------
    // Read
    // ---------------------------------------------------------------------

    public Task<IReadOnlyList<Anagrafica>> ElencoAsync(CancellationToken cancellationToken = default)
        => _repository.GetAttiviAsync(cancellationToken);

    public Task<Anagrafica?> GetByIdAsync(Guid idAnagrafica, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(idAnagrafica, cancellationToken);

    // ---------------------------------------------------------------------
    // Create
    // ---------------------------------------------------------------------

    public async Task<Guid> CreaAsync(Anagrafica anagrafica, CancellationToken cancellationToken = default)
    {
        ValidaCampiObbligatori(anagrafica);

        // Generazione PK app-side (GUID UUIDv7 time-ordered, ADR D22): l'Id è
        // disponibile prima dell'INSERT, niente IDENTITY/OUTPUT.
        anagrafica.IdAnagrafica = Guid.CreateVersion7();

        try
        {
            await _repository.InsertAsync(anagrafica, cancellationToken);
            // Snapshot completo del nuovo record (la sanitizzazione dei segreti
            // è in AuditDettaglio; l'Anagrafica comunque non ne contiene).
            await _audit.RegistraCreazioneAsync(EntityType, anagrafica.IdAnagrafica,
                anagrafica.RagioneSociale, AuditDettaglio.Snapshot(anagrafica), cancellationToken);
            return anagrafica.IdAnagrafica;
        }
        catch (SqlException ex) when (ex.Number == SqlErrorConstraintViolation)
        {
            throw TraduciViolazioneFk(ex);
        }
    }

    // ---------------------------------------------------------------------
    // Update
    // ---------------------------------------------------------------------

    public async Task AggiornaAsync(Anagrafica anagrafica, CancellationToken cancellationToken = default)
    {
        ValidaCampiObbligatori(anagrafica);

        // Stato precedente per il diff dell'audit (cosa è cambiato). Letto prima
        // dell'update; se non trovato si ripiega sullo snapshot del nuovo stato.
        var precedente = await _repository.GetByIdAsync(anagrafica.IdAnagrafica, cancellationToken);
        try
        {
            await _repository.UpdateAsync(anagrafica, cancellationToken);
            var dati = precedente is null
                ? AuditDettaglio.Snapshot(anagrafica)
                : AuditDettaglio.Diff(precedente, anagrafica);
            await _audit.RegistraModificaAsync(EntityType, anagrafica.IdAnagrafica,
                anagrafica.RagioneSociale, dati, cancellationToken);
        }
        catch (SqlException ex) when (ex.Number == SqlErrorConstraintViolation)
        {
            throw TraduciViolazioneFk(ex);
        }
    }

    // ---------------------------------------------------------------------
    // Delete (con doppia difesa)
    // ---------------------------------------------------------------------

    public async Task EliminaAsync(Guid idAnagrafica, CancellationToken cancellationToken = default)
    {
        // Pre-check: messaggio specifico per l'utente. Con il soft-delete
        // (ADR D22) non c'è violazione FK a valle da intercettare — disattivare
        // una riga non rompe l'integrità referenziale — ma manteniamo il
        // pre-check per la regola di dominio "non disattivare un cliente con
        // attività collegate" (dispensa cap. 3.4) e per il pattern
        // visibility-driven della UI.
        if (await _repository.HasDipendenzeAsync(idAnagrafica, cancellationToken))
        {
            throw new AnagraficaConDipendenzeException(idAnagrafica);
        }

        // Cattura la ragione sociale prima della disattivazione, per una riga di
        // audit leggibile (l'id da solo non racconta "cosa" è stato eliminato).
        var anagrafica = await _repository.GetByIdAsync(idAnagrafica, cancellationToken);

        await _repository.DisattivaAsync(idAnagrafica, cancellationToken);
        // Snapshot del record eliminato (così resta traccia di cosa è sparito).
        var dati = anagrafica is null ? null : AuditDettaglio.Snapshot(anagrafica);
        await _audit.RegistraEliminazioneAsync(EntityType, idAnagrafica, anagrafica?.RagioneSociale, dati, cancellationToken);
    }

    public async Task<bool> EEliminabileAsync(Guid idAnagrafica, CancellationToken cancellationToken = default)
        => !await _repository.HasDipendenzeAsync(idAnagrafica, cancellationToken);

    // ---------------------------------------------------------------------
    // Helper privati
    // ---------------------------------------------------------------------

    /// <summary>
    /// Validazione di forma. L'ordine dei controlli è quello che
    /// l'utente vedrà come motivo del fallimento (CLAUDE.md "Ordine
    /// dei controlli in eccezioni tipizzate è UX").
    /// </summary>
    private static void ValidaCampiObbligatori(Anagrafica anagrafica)
    {
        if (string.IsNullOrWhiteSpace(anagrafica.RagioneSociale))
        {
            throw new AnagraficaInvalidaException(
                AnagraficaInvalidaMotivo.RagioneSocialeObbligatoria,
                "La ragione sociale è obbligatoria.");
        }
        // Note: Paese/Provincia non validati qui — la UI propone solo
        // valori dei lookup e il DB ha la FK come ultimo presidio.
        // Eventuale violation viene tradotta in TraduciViolazioneFk.
    }

    /// <summary>
    /// Mappa una <see cref="SqlException"/> di constraint violation
    /// (errore 547) sull'eccezione di dominio corretta. Il nome del
    /// vincolo è nel messaggio dell'errore SQL.
    /// </summary>
    private static AnagraficaInvalidaException TraduciViolazioneFk(SqlException ex)
    {
        // SQL Server include il nome del vincolo nel messaggio: cerchiamo
        // per substring perché il testo localizzato cambia tra ambienti.
        var msg = ex.Message;
        if (msg.Contains("FK_Anagrafica_Paesi", StringComparison.OrdinalIgnoreCase))
        {
            return new AnagraficaInvalidaException(
                AnagraficaInvalidaMotivo.PaeseInesistente,
                "Il paese indicato non è valido. Scegliere un paese dall'elenco.",
                ex);
        }
        if (msg.Contains("FK_Anagrafica_Province", StringComparison.OrdinalIgnoreCase))
        {
            return new AnagraficaInvalidaException(
                AnagraficaInvalidaMotivo.ProvinciaInesistente,
                "La provincia indicata non è valida. Scegliere una provincia dall'elenco.",
                ex);
        }
        // Vincolo inatteso: rilanciamo come PaeseInesistente di fallback,
        // ma include l'errore originale come InnerException per la diagnosi.
        return new AnagraficaInvalidaException(
            AnagraficaInvalidaMotivo.PaeseInesistente,
            "Violazione di un vincolo del database.",
            ex);
    }
}
