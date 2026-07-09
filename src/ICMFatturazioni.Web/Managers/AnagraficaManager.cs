using ICMFatturazioni.Web.Auditing;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Models;
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
    // Manager dei cataloghi referenziati: servono a validare i puntatori
    // (esistenza + IsAttivo) e la coerenza FlagBanca↔banca PRIMA di scrivere.
    // Dipendenza manager→manager già usata altrove (es. BancaAppoggioManager):
    // nessun ciclo, questi non dipendono a loro volta da AnagraficaManager.
    private readonly ICodicePagamentoManager _codicePagamento;
    private readonly IBancaAppoggioManager _bancaAppoggio;
    private readonly ICodiceIVAManager _codiceIva;

    public AnagraficaManager(
        IAnagraficaRepository repository,
        IAuditManager audit,
        ICodicePagamentoManager codicePagamento,
        IBancaAppoggioManager bancaAppoggio,
        ICodiceIVAManager codiceIva)
    {
        _repository = repository;
        _audit = audit;
        _codicePagamento = codicePagamento;
        _bancaAppoggio = bancaAppoggio;
        _codiceIva = codiceIva;
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
        // disponibile prima dell'INSERT, niente IDENTITY/OUTPUT. La generiamo
        // PRIMA di validare i riferimenti perché la coerenza del caso "pagamento
        // di tipo cliente" confronta banca.IdCliente con questo IdAnagrafica.
        anagrafica.IdAnagrafica = Guid.CreateVersion7();

        await ValidaRiferimentiAsync(anagrafica, cancellationToken);

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
        await ValidaRiferimentiAsync(anagrafica, cancellationToken);

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
        // Enti pubblici non supportati: la generazione dei tracciati XML verso la
        // PA (FPA12, split payment) non è implementata. Blocco categorico — vale
        // per creazione e modifica — allineato allo stand-by del ramo fiscale PA.
        if (anagrafica.TipoAnagrafica == TipoAnagrafica.EntePubblico)
        {
            throw new AnagraficaInvalidaException(
                AnagraficaInvalidaMotivo.EntePubblicoNonSupportato,
                "Non è possibile creare un'anagrafica di tipo «Ente pubblico»: la generazione "
                + "dei tracciati per la Pubblica Amministrazione non è ancora implementata. "
                + "Sono ammesse solo anagrafiche Privato o Società.");
        }

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
    /// Valida i puntatori amministrativi (Pagamento / Banca / Codice IVA) quando
    /// valorizzati: esistenza + <c>IsAttivo</c> e, per la coppia pagamento+banca,
    /// la coerenza col <c>FlagBanca</c> del tipo di pagamento.
    /// </summary>
    /// <remarks>
    /// Pre-check applicativo (UX: messaggio specifico). La correttezza sotto race
    /// condition è garantita anche dalle FK di migration 023, tradotte da
    /// <see cref="TraduciViolazioneFk"/> (pattern "doppia difesa"). La sola regola
    /// senza sentinel a DB è la coerenza FlagBanca↔banca: incrocia due tabelle,
    /// non è esprimibile come FK, quindi vive unicamente qui.
    /// </remarks>
    private async Task ValidaRiferimentiAsync(Anagrafica anagrafica, CancellationToken cancellationToken)
    {
        // Pagamento: l'elenco contiene i soli attivi → id assente = inesistente o
        // disattivato (in entrambi i casi non selezionabile). La Riga porta il
        // FlagBanca, che serve subito dopo per la coerenza con la banca.
        CodicePagamentoRiga? pagamento = null;
        if (anagrafica.IdPag is Guid idPag)
        {
            var pagamenti = await _codicePagamento.ElencoAsync(cancellationToken);
            pagamento = pagamenti.FirstOrDefault(p => p.IdCodicePagamento == idPag);
            if (pagamento is null)
            {
                throw new AnagraficaInvalidaException(
                    AnagraficaInvalidaMotivo.PagamentoInesistente,
                    "Il codice di pagamento selezionato non è più disponibile. Sceglierne un altro dall'elenco.");
            }
        }

        BancaAppoggioRiga? banca = null;
        if (anagrafica.IdBancaAppoggio is Guid idBanca)
        {
            banca = await _bancaAppoggio.GetByIdAsync(idBanca, cancellationToken);
            if (banca is null || !banca.IsAttivo)
            {
                throw new AnagraficaInvalidaException(
                    AnagraficaInvalidaMotivo.BancaInesistente,
                    "La banca di appoggio selezionata non è più disponibile. Sceglierne un'altra dall'elenco.");
            }
        }

        // Coerenza FlagBanca↔banca: solo se entrambi valorizzati (una banca senza
        // pagamento non ha un flag da rispettare).
        if (pagamento is not null && banca is not null)
        {
            var coerente = pagamento.FlagBanca switch
            {
                // Pagamento "dati azienda" (es. bonifico): serve una banca aziendale.
                FlagBanca.Azienda => banca.IsBancaAzienda,
                // Pagamento "dati cliente" (es. ricevuta bancaria): serve una banca
                // di QUESTO cliente (non dell'azienda, non di un altro cliente).
                FlagBanca.Cliente => banca.IdCliente == anagrafica.IdAnagrafica,
                _ => false,
            };
            if (!coerente)
            {
                throw new AnagraficaInvalidaException(
                    AnagraficaInvalidaMotivo.BancaNonCoerenteColPagamento,
                    pagamento.FlagBanca == FlagBanca.Azienda
                        ? "Il pagamento scelto usa i dati bancari dell'azienda: selezionare una banca aziendale."
                        : "Il pagamento scelto usa i dati bancari del cliente: selezionare una banca di questo cliente.");
            }
        }

        if (anagrafica.IdCodiciIVA is Guid idIva)
        {
            var iva = await _codiceIva.GetByIdAsync(idIva, cancellationToken);
            if (iva is null || !iva.IsAttivo)
            {
                throw new AnagraficaInvalidaException(
                    AnagraficaInvalidaMotivo.CodiceIVAInesistente,
                    "Il codice IVA selezionato non è più disponibile. Sceglierne un altro dall'elenco.");
            }
        }
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
        // FK dei cataloghi (migration 023): sentinel della "doppia difesa". Se il
        // riferimento sparisce tra il pre-check e l'INSERT/UPDATE, la FK scatta
        // qui e produce lo stesso messaggio del pre-check.
        if (msg.Contains("FK_Anagrafica_CodicePagamento", StringComparison.OrdinalIgnoreCase))
        {
            return new AnagraficaInvalidaException(
                AnagraficaInvalidaMotivo.PagamentoInesistente,
                "Il codice di pagamento selezionato non è più disponibile. Sceglierne un altro dall'elenco.",
                ex);
        }
        if (msg.Contains("FK_Anagrafica_BancaAppoggio", StringComparison.OrdinalIgnoreCase))
        {
            return new AnagraficaInvalidaException(
                AnagraficaInvalidaMotivo.BancaInesistente,
                "La banca di appoggio selezionata non è più disponibile. Sceglierne un'altra dall'elenco.",
                ex);
        }
        if (msg.Contains("FK_Anagrafica_CodiceIVA", StringComparison.OrdinalIgnoreCase))
        {
            return new AnagraficaInvalidaException(
                AnagraficaInvalidaMotivo.CodiceIVAInesistente,
                "Il codice IVA selezionato non è più disponibile. Sceglierne un altro dall'elenco.",
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
