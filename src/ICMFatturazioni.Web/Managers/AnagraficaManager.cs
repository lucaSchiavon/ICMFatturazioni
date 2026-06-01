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

    private readonly IAnagraficaRepository _repository;

    public AnagraficaManager(IAnagraficaRepository repository)
    {
        _repository = repository;
    }

    // ---------------------------------------------------------------------
    // Read
    // ---------------------------------------------------------------------

    public Task<IReadOnlyList<Anagrafica>> ElencoAsync(CancellationToken cancellationToken = default)
        => _repository.GetAllAsync(cancellationToken);

    public Task<Anagrafica?> GetByIdAsync(int idAnagrafica, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(idAnagrafica, cancellationToken);

    // ---------------------------------------------------------------------
    // Create
    // ---------------------------------------------------------------------

    public async Task<int> CreaAsync(Anagrafica anagrafica, CancellationToken cancellationToken = default)
    {
        ValidaCampiObbligatori(anagrafica);
        try
        {
            return await _repository.InsertAsync(anagrafica, cancellationToken);
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
        try
        {
            await _repository.UpdateAsync(anagrafica, cancellationToken);
        }
        catch (SqlException ex) when (ex.Number == SqlErrorConstraintViolation)
        {
            throw TraduciViolazioneFk(ex);
        }
    }

    // ---------------------------------------------------------------------
    // Delete (con doppia difesa)
    // ---------------------------------------------------------------------

    public async Task EliminaAsync(int idAnagrafica, CancellationToken cancellationToken = default)
    {
        // Pre-check: messaggio specifico per l'utente.
        if (await _repository.HasDipendenzeAsync(idAnagrafica, cancellationToken))
        {
            throw new AnagraficaConDipendenzeException(idAnagrafica);
        }

        // Sentinel: in Fase 3+ il DELETE potrà fallire per FK violations a
        // valle (DELETE su anagrafica referenziata da un progetto). Quando
        // accadrà, intercettiamo il 547 e rilanciamo con l'eccezione
        // tipizzata. Pattern doppia difesa: il pre-check copre la UX, il
        // catch copre le race condition (qualcuno crea un progetto fra il
        // pre-check e il DELETE).
        try
        {
            await _repository.DeleteAsync(idAnagrafica, cancellationToken);
        }
        catch (SqlException ex) when (ex.Number == SqlErrorConstraintViolation)
        {
            throw new AnagraficaConDipendenzeException(idAnagrafica);
        }
    }

    public async Task<bool> EEliminabileAsync(int idAnagrafica, CancellationToken cancellationToken = default)
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
