using ICMFatturazioni.Web.Auditing;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Repositories.Interfaces;
using Microsoft.Data.SqlClient;

namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Implementazione di <see cref="ICodiceIVAManager"/>. Concentra qui le regole
/// di business:
///   1) validazione dei campi obbligatori (Codice, Descrizione) e dell'aliquota,
///   2) regola condizionale Natura ⟺ Aliquota = 0 (dispensa §6.2),
///   3) unicità del Codice tra gli attivi (pre-check + indice UNIQUE filtrato),
///   4) traduzione delle SqlException (FK Natura / CHECK / unique) in
///      <see cref="CodiceIVAInvalidaException"/> con motivo specifico,
///   5) pattern "doppia difesa" sulla DELETE,
///   6) audit di ogni scrittura (Regola 7: snapshot su create/delete, diff su update).
/// </summary>
internal sealed class CodiceIVAManager : ICodiceIVAManager
{
    // 547  = constraint violation (FK_CodiciIVA_Natura o CK_CodiciIVA_NaturaAliquota).
    // 2601 = tentativo di chiave duplicata in indice UNIQUE (UX_CodiciIVA_Codice, filtrato).
    // 2627 = violazione di UNIQUE constraint (incluso per robustezza).
    private const int SqlErrorConstraintViolation = 547;
    private const int SqlErrorDuplicateIndexKey = 2601;
    private const int SqlErrorDuplicateConstraint = 2627;

    // EntityType usato nelle righe di audit per questa entità.
    private const string EntityType = nameof(CodiceIVA);

    private readonly ICodiceIVARepository _repository;
    private readonly IAuditManager _audit;

    public CodiceIVAManager(ICodiceIVARepository repository, IAuditManager audit)
    {
        _repository = repository;
        _audit = audit;
    }

    // ---------------------------------------------------------------------
    // Read
    // ---------------------------------------------------------------------

    public Task<IReadOnlyList<CodiceIVA>> ElencoAsync(CancellationToken cancellationToken = default)
        => _repository.GetAttiviAsync(cancellationToken);

    public Task<CodiceIVA?> GetByIdAsync(Guid idCodiceIVA, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(idCodiceIVA, cancellationToken);

    // ---------------------------------------------------------------------
    // Create
    // ---------------------------------------------------------------------

    public async Task<Guid> CreaAsync(CodiceIVA codiceIva, CancellationToken cancellationToken = default)
    {
        var normalizzato = Normalizza(codiceIva);
        ValidaCampi(normalizzato);
        await ValidaUnicitaCodiceAsync(normalizzato.Codice, escludiId: null, cancellationToken);

        // Generazione PK app-side (GUID UUIDv7 time-ordered, ADR D22).
        normalizzato.IdCodiceIVA = Guid.CreateVersion7();

        try
        {
            await _repository.InsertAsync(normalizzato, cancellationToken);
            await _audit.RegistraCreazioneAsync(EntityType, normalizzato.IdCodiceIVA,
                normalizzato.Codice, AuditDettaglio.Snapshot(normalizzato), cancellationToken);
            return normalizzato.IdCodiceIVA;
        }
        catch (SqlException ex) when (IsViolazioneVincolo(ex))
        {
            throw TraduciViolazione(ex);
        }
    }

    // ---------------------------------------------------------------------
    // Update
    // ---------------------------------------------------------------------

    public async Task AggiornaAsync(CodiceIVA codiceIva, CancellationToken cancellationToken = default)
    {
        var normalizzato = Normalizza(codiceIva);
        ValidaCampi(normalizzato);
        await ValidaUnicitaCodiceAsync(normalizzato.Codice, escludiId: normalizzato.IdCodiceIVA, cancellationToken);

        // Stato precedente per il diff dell'audit; se non trovato si ripiega
        // sullo snapshot del nuovo stato.
        var precedente = await _repository.GetByIdAsync(normalizzato.IdCodiceIVA, cancellationToken);
        try
        {
            await _repository.UpdateAsync(normalizzato, cancellationToken);
            var dati = precedente is null
                ? AuditDettaglio.Snapshot(normalizzato)
                : AuditDettaglio.Diff(precedente, normalizzato);
            await _audit.RegistraModificaAsync(EntityType, normalizzato.IdCodiceIVA,
                normalizzato.Codice, dati, cancellationToken);
        }
        catch (SqlException ex) when (IsViolazioneVincolo(ex))
        {
            throw TraduciViolazione(ex);
        }
    }

    // ---------------------------------------------------------------------
    // Delete (con doppia difesa)
    // ---------------------------------------------------------------------

    public async Task EliminaAsync(Guid idCodiceIVA, CancellationToken cancellationToken = default)
    {
        // Pre-check: un codice IVA usato da anagrafiche attive non si disattiva
        // (lascerebbe puntatori a un codice non più selezionabile).
        if (await _repository.HasDipendenzeAsync(idCodiceIVA, cancellationToken))
        {
            throw new CodiceIVAConDipendenzeException(idCodiceIVA);
        }

        // Snapshot del record prima della disattivazione, per una riga di audit
        // leggibile e per tracciare cosa è stato eliminato.
        var codice = await _repository.GetByIdAsync(idCodiceIVA, cancellationToken);

        await _repository.DisattivaAsync(idCodiceIVA, cancellationToken);
        var dati = codice is null ? null : AuditDettaglio.Snapshot(codice);
        await _audit.RegistraEliminazioneAsync(EntityType, idCodiceIVA, codice?.Codice, dati, cancellationToken);
    }

    public async Task<bool> EEliminabileAsync(Guid idCodiceIVA, CancellationToken cancellationToken = default)
        => !await _repository.HasDipendenzeAsync(idCodiceIVA, cancellationToken);

    // ---------------------------------------------------------------------
    // Helper privati
    // ---------------------------------------------------------------------

    /// <summary>
    /// Normalizza l'input prima di validare/persistere: trim di Codice e
    /// Descrizione; Natura ridotta a <c>null</c> se vuota/whitespace (così non
    /// viola il CHECK quando l'aliquota è imponibile); Obbligo bollo forzato a
    /// <c>null</c> per le aliquote imponibili (è pertinente solo alle operazioni
    /// non imponibili, aliquota = 0). Per le esenti il valore tri-state
    /// (null/sì/no) si conserva così com'è. L'entità è immutabile sui campi di
    /// contenuto, quindi si ricostruisce una copia.
    /// </summary>
    private static CodiceIVA Normalizza(CodiceIVA c) => new()
    {
        IdCodiceIVA  = c.IdCodiceIVA,
        Codice       = c.Codice?.Trim() ?? string.Empty,
        Descrizione  = c.Descrizione?.Trim() ?? string.Empty,
        Aliquota     = c.Aliquota,
        Natura       = string.IsNullOrWhiteSpace(c.Natura) ? null : c.Natura.Trim(),
        ObbligoBollo = c.Aliquota == 0 ? c.ObbligoBollo : null,
        IsAttivo     = c.IsAttivo,
    };

    /// <summary>
    /// Validazione di forma + regola condizionale. L'ordine dei controlli è
    /// quello che l'utente vedrà come motivo del fallimento (CLAUDE.md "Ordine
    /// dei controlli in eccezioni tipizzate è UX").
    /// </summary>
    private static void ValidaCampi(CodiceIVA c)
    {
        if (string.IsNullOrWhiteSpace(c.Codice))
        {
            throw new CodiceIVAInvalidaException(
                CodiceIVAInvalidaMotivo.CodiceObbligatorio,
                "Il codice (sigla) è obbligatorio.");
        }
        if (string.IsNullOrWhiteSpace(c.Descrizione))
        {
            throw new CodiceIVAInvalidaException(
                CodiceIVAInvalidaMotivo.DescrizioneObbligatoria,
                "La descrizione è obbligatoria (appare in fattura).");
        }
        if (c.Aliquota < 0)
        {
            throw new CodiceIVAInvalidaException(
                CodiceIVAInvalidaMotivo.AliquotaNonValida,
                "L'aliquota non può essere negativa.");
        }

        // Regola condizionale (dispensa §6.2): la Natura esiste ⟺ Aliquota = 0.
        var haNatura = !string.IsNullOrWhiteSpace(c.Natura);
        if (c.Aliquota == 0 && !haNatura)
        {
            throw new CodiceIVAInvalidaException(
                CodiceIVAInvalidaMotivo.NaturaObbligatoria,
                "Per un'aliquota pari a 0 la Natura IVA è obbligatoria.");
        }
        if (c.Aliquota > 0 && haNatura)
        {
            throw new CodiceIVAInvalidaException(
                CodiceIVAInvalidaMotivo.NaturaNonAmmessa,
                "La Natura IVA va indicata solo per le aliquote pari a 0.");
        }

        // Per le operazioni a 0 l'Obbligo bollo è una scelta obbligatoria
        // (Sì/No): il "non impostato" (null) non è ammesso. Per le imponibili è
        // stato già normalizzato a null e qui non si controlla.
        if (c.Aliquota == 0 && c.ObbligoBollo is null)
        {
            throw new CodiceIVAInvalidaException(
                CodiceIVAInvalidaMotivo.ObbligoBolloObbligatorio,
                "Per un'aliquota pari a 0 occorre indicare se il bollo è dovuto (Sì o No).");
        }
    }

    /// <summary>
    /// Pre-check di unicità del Codice tra gli attivi. Il presidio definitivo è
    /// l'indice UNIQUE filtrato <c>UX_CodiciIVA_Codice</c> (doppia difesa).
    /// </summary>
    private async Task ValidaUnicitaCodiceAsync(string codice, Guid? escludiId, CancellationToken cancellationToken)
    {
        if (await _repository.ExistsCodiceAttivoAsync(codice, escludiId, cancellationToken))
        {
            throw new CodiceIVAInvalidaException(
                CodiceIVAInvalidaMotivo.CodiceDuplicato,
                $"Esiste già un codice IVA attivo con sigla \"{codice}\".");
        }
    }

    private static bool IsViolazioneVincolo(SqlException ex)
        => ex.Number is SqlErrorConstraintViolation or SqlErrorDuplicateIndexKey or SqlErrorDuplicateConstraint;

    /// <summary>
    /// Mappa una <see cref="SqlException"/> di violazione vincolo sull'eccezione
    /// di dominio corretta. Il nome del vincolo/indice è nel messaggio SQL: lo
    /// cerchiamo per substring perché il testo localizzato cambia tra ambienti.
    /// Sono casi di "doppia difesa": il manager pre-valida, qui restiamo
    /// corretti anche sotto race condition.
    /// </summary>
    private static CodiceIVAInvalidaException TraduciViolazione(SqlException ex)
    {
        var msg = ex.Message;
        if (msg.Contains("UX_CodiciIVA_Codice", StringComparison.OrdinalIgnoreCase))
        {
            return new CodiceIVAInvalidaException(
                CodiceIVAInvalidaMotivo.CodiceDuplicato,
                "Esiste già un codice IVA attivo con questa sigla.", ex);
        }
        if (msg.Contains("FK_CodiciIVA_Natura", StringComparison.OrdinalIgnoreCase))
        {
            return new CodiceIVAInvalidaException(
                CodiceIVAInvalidaMotivo.NaturaInesistente,
                "La Natura IVA indicata non è valida. Sceglierla dall'elenco.", ex);
        }
        // CK_CodiciIVA_NaturaAliquota o vincolo inatteso: la combinazione
        // Aliquota/Natura non rispetta la regola. Fallback generico.
        return new CodiceIVAInvalidaException(
            CodiceIVAInvalidaMotivo.NaturaNonAmmessa,
            "La combinazione di aliquota e Natura IVA non è valida.", ex);
    }
}
