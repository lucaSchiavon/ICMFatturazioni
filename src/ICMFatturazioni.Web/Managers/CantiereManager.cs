using ICMFatturazioni.Web.Auditing;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Repositories.Interfaces;
using Microsoft.Data.SqlClient;

namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Implementazione di <see cref="ICantiereManager"/>.
/// Regole di business:
///   1) validazione campi obbligatori e lunghezze (colonne di dbo.Cantiere),
///   2) pre-check che l'attività associata esista e sia attiva
///      (FK_Cantiere_Progetto come sentinel — pattern "doppia difesa"),
///   3) audit su fatt.Audit per ogni scrittura (snapshot/diff).
/// L'eliminazione è un soft-delete senza pre-check di dipendenze: disattivare
/// un cantiere non rompe la FK dei verbali che lo referenziano (la riga resta),
/// coerente con ICMVerbali che gestisce IsAttivo liberamente da checkbox.
/// </summary>
internal sealed class CantiereManager : ICantiereManager
{
    // 547 = constraint violation (FK). Sul Cantiere l'unica FK è
    // FK_Cantiere_Progetto (dbo.Cantiere.ProgettoId → dbo.Progetto.Id).
    private const int SqlErrorConstraintViolation = 547;

    // Lunghezze massime delle colonne di dbo.Cantiere (migration 001 di ICMVerbali).
    private const int MaxUbicazione = 300;
    private const int MaxTipologia = 500;

    private const string EntityType = nameof(Cantiere);

    private readonly ICantiereRepository _repository;
    private readonly IAuditManager _audit;
    // Manager dell'entità referenziata: valida il puntatore (esistenza + IsAttivo)
    // PRIMA di scrivere. Dipendenza manager→manager già usata altrove
    // (es. AnagraficaManager): nessun ciclo, AttivitaManager non dipende da questo.
    private readonly IAttivitaManager _attivita;

    public CantiereManager(
        ICantiereRepository repository,
        IAuditManager audit,
        IAttivitaManager attivita)
    {
        _repository = repository;
        _audit = audit;
        _attivita = attivita;
    }

    // ---------------------------------------------------------------------
    // Read
    // ---------------------------------------------------------------------

    public Task<IReadOnlyList<Cantiere>> ElencoAsync(CancellationToken cancellationToken = default)
        => _repository.GetAttiviAsync(cancellationToken);

    public Task<IReadOnlyList<Cantiere>> ElencoPerAttivitaAsync(Guid idAttivita, CancellationToken cancellationToken = default)
        => _repository.GetByAttivitaAsync(idAttivita, cancellationToken);

    public Task<Cantiere?> GetByIdAsync(Guid idCantiere, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(idCantiere, cancellationToken);

    // ---------------------------------------------------------------------
    // Create
    // ---------------------------------------------------------------------

    public async Task<Guid> CreaAsync(Cantiere cantiere, CancellationToken cancellationToken = default)
    {
        ValidaCampi(cantiere);
        await ValidaAttivitaAsync(cantiere.IdAttivita, cancellationToken);

        // Generazione PK app-side (GUID UUIDv7 time-ordered, ADR D22).
        cantiere.IdCantiere = Guid.CreateVersion7();

        try
        {
            await _repository.InsertAsync(cantiere, cancellationToken);
            await _audit.RegistraCreazioneAsync(EntityType, cantiere.IdCantiere,
                cantiere.Ubicazione, AuditDettaglio.Snapshot(cantiere), cancellationToken);
            return cantiere.IdCantiere;
        }
        catch (SqlException ex) when (ex.Number == SqlErrorConstraintViolation)
        {
            throw TraduciViolazioneFk(ex);
        }
    }

    // ---------------------------------------------------------------------
    // Update
    // ---------------------------------------------------------------------

    public async Task AggiornaAsync(Cantiere cantiere, CancellationToken cancellationToken = default)
    {
        ValidaCampi(cantiere);
        await ValidaAttivitaAsync(cantiere.IdAttivita, cancellationToken);

        // Stato precedente per il diff dell'audit (cosa è cambiato). Letto prima
        // dell'update; se non trovato si ripiega sullo snapshot del nuovo stato.
        var precedente = await _repository.GetByIdAsync(cantiere.IdCantiere, cancellationToken);
        try
        {
            await _repository.UpdateAsync(cantiere, cancellationToken);
            var dati = precedente is null
                ? AuditDettaglio.Snapshot(cantiere)
                : AuditDettaglio.Diff(precedente, cantiere);
            await _audit.RegistraModificaAsync(EntityType, cantiere.IdCantiere,
                cantiere.Ubicazione, dati, cancellationToken);
        }
        catch (SqlException ex) when (ex.Number == SqlErrorConstraintViolation)
        {
            throw TraduciViolazioneFk(ex);
        }
    }

    // ---------------------------------------------------------------------
    // Delete (soft)
    // ---------------------------------------------------------------------

    public async Task EliminaAsync(Guid idCantiere, CancellationToken cancellationToken = default)
    {
        // Cattura lo stato prima della disattivazione, per una riga di audit
        // leggibile (l'id da solo non racconta "cosa" è stato eliminato).
        var cantiere = await _repository.GetByIdAsync(idCantiere, cancellationToken);

        await _repository.DisattivaAsync(idCantiere, cancellationToken);
        var dati = cantiere is null ? null : AuditDettaglio.Snapshot(cantiere);
        await _audit.RegistraEliminazioneAsync(EntityType, idCantiere, cantiere?.Ubicazione, dati, cancellationToken);
    }

    // ---------------------------------------------------------------------
    // Helper privati
    // ---------------------------------------------------------------------

    /// <summary>
    /// Validazione di forma. L'ordine dei controlli è quello che l'utente vedrà
    /// come motivo del fallimento (CLAUDE.md "Ordine dei controlli in eccezioni
    /// tipizzate è UX"): prima l'attività (senza non ha senso il resto), poi i
    /// campi descrittivi, infine l'importo facoltativo.
    /// </summary>
    private static void ValidaCampi(Cantiere cantiere)
    {
        if (cantiere.IdAttivita == Guid.Empty)
        {
            throw new CantiereInvalidoException(
                CantiereInvalidoMotivo.AttivitaObbligatoria,
                "L'attività è obbligatoria: selezionarne una dall'elenco.");
        }
        if (string.IsNullOrWhiteSpace(cantiere.Ubicazione))
        {
            throw new CantiereInvalidoException(
                CantiereInvalidoMotivo.UbicazioneObbligatoria,
                "L'ubicazione del cantiere è obbligatoria.");
        }
        if (cantiere.Ubicazione.Length > MaxUbicazione)
        {
            throw new CantiereInvalidoException(
                CantiereInvalidoMotivo.UbicazioneTroppoLunga,
                $"L'ubicazione non può superare {MaxUbicazione} caratteri.");
        }
        if (string.IsNullOrWhiteSpace(cantiere.Tipologia))
        {
            throw new CantiereInvalidoException(
                CantiereInvalidoMotivo.TipologiaObbligatoria,
                "La tipologia dei lavori è obbligatoria.");
        }
        if (cantiere.Tipologia.Length > MaxTipologia)
        {
            throw new CantiereInvalidoException(
                CantiereInvalidoMotivo.TipologiaTroppoLunga,
                $"La tipologia non può superare {MaxTipologia} caratteri.");
        }
        if (cantiere.ImportoAppalto is < 0)
        {
            throw new CantiereInvalidoException(
                CantiereInvalidoMotivo.ImportoNegativo,
                "L'importo appalto non può essere negativo.");
        }
    }

    /// <summary>
    /// Pre-check applicativo: l'attività deve esistere ed essere attiva
    /// (UX: messaggio specifico). La correttezza sotto race condition è
    /// garantita dalla FK_Cantiere_Progetto, tradotta da
    /// <see cref="TraduciViolazioneFk"/> (pattern "doppia difesa").
    /// </summary>
    private async Task ValidaAttivitaAsync(Guid idAttivita, CancellationToken cancellationToken)
    {
        var attivita = await _attivita.GetByIdAsync(idAttivita, cancellationToken);
        if (attivita is null || !attivita.IsAttivo)
        {
            throw new CantiereInvalidoException(
                CantiereInvalidoMotivo.AttivitaInesistente,
                "L'attività selezionata non è più disponibile. Sceglierne un'altra dall'elenco.");
        }
    }

    /// <summary>
    /// Mappa una <see cref="SqlException"/> di constraint violation (errore 547)
    /// sull'eccezione di dominio. L'unica FK del Cantiere è quella verso
    /// l'attività (FK_Cantiere_Progetto sulla tabella base dbo.Cantiere).
    /// </summary>
    private static CantiereInvalidoException TraduciViolazioneFk(SqlException ex)
        => new(
            CantiereInvalidoMotivo.AttivitaInesistente,
            "L'attività selezionata non è più disponibile. Sceglierne un'altra dall'elenco.",
            ex);
}
