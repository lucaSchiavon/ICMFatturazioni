using ICMFatturazioni.Web.Auditing;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Models;
using ICMFatturazioni.Web.Repositories.Interfaces;
using ICMFatturazioni.Web.Validation;
using Microsoft.Data.SqlClient;

namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Implementazione di <see cref="IBancaAppoggioManager"/>. Orchestrazione del
/// modello normalizzato:
///   1) normalizza l'input (trim, IBAN, IBAN solo azienda),
///   2) valida (Banca obbligatoria, formato ABI/CAB, IBAN formato+checksum e
///      coerenza ABI/CAB ⟷ IBAN),
///   3) risolve banca e agenzia con get-or-create (aggiornando ABI/CAB se
///      modificati) tramite i rispettivi manager,
///   4) anti-duplicato del legame (intestatario+banca+agenzia),
///   5) scrive il legame e ne registra l'audit (Regola 7),
///   6) pattern "doppia difesa" su DELETE.
/// </summary>
internal sealed class BancaAppoggioManager : IBancaAppoggioManager
{
    // 547  = FK violation (FK_BancheAppoggio_Cliente).
    // 2601 = chiave duplicata in indice UNIQUE (UX_BancheAppoggio_ClienteBancaAgenzia).
    // 2627 = UNIQUE constraint.
    private const int SqlErrorConstraintViolation = 547;
    private const int SqlErrorDuplicateIndexKey = 2601;
    private const int SqlErrorDuplicateConstraint = 2627;

    private const string EntityType = nameof(BancaAppoggio);

    private readonly IBancaAppoggioRepository _repository;
    private readonly IBancaManager _bancaManager;
    private readonly IAgenziaManager _agenziaManager;
    private readonly IAuditManager _audit;

    public BancaAppoggioManager(
        IBancaAppoggioRepository repository,
        IBancaManager bancaManager,
        IAgenziaManager agenziaManager,
        IAuditManager audit)
    {
        _repository = repository;
        _bancaManager = bancaManager;
        _agenziaManager = agenziaManager;
        _audit = audit;
    }

    // ---------------------------------------------------------------------
    // Read
    // ---------------------------------------------------------------------

    public Task<IReadOnlyList<BancaAppoggioRiga>> ElencoAsync(CancellationToken cancellationToken = default)
        => _repository.GetAttiviAsync(cancellationToken);

    public Task<BancaAppoggioRiga?> GetByIdAsync(Guid idBancaAppoggio, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(idBancaAppoggio, cancellationToken);

    public Task<IReadOnlyList<BancaAppoggioRiga>> SelezionabiliAsync(Guid? idCliente, bool bancheAzienda, CancellationToken cancellationToken = default)
        => _repository.GetSelezionabiliAsync(idCliente, bancheAzienda, cancellationToken);

    // ---------------------------------------------------------------------
    // Create
    // ---------------------------------------------------------------------

    public async Task<Guid> CreaAsync(BancaAppoggioInput input, CancellationToken cancellationToken = default)
    {
        var norm = Normalizza(input);
        ValidaInput(norm);

        // Unicità IBAN prima del get-or-create di banca/agenzia: un IBAN
        // duplicato non deve lasciare anagrafiche bancarie orfane.
        await ValidaIbanUnicoAsync(norm, escludiId: null, cancellationToken);

        var (idBanca, idAgenzia) = await RisolviBancaAgenziaAsync(norm, cancellationToken);

        if (await _repository.ExistsLegameAttivoAsync(norm.IdCliente, idBanca, idAgenzia, escludiId: null, cancellationToken))
        {
            throw LegameDuplicato();
        }

        var entity = new BancaAppoggio
        {
            IdBancaAppoggio = Guid.CreateVersion7(),
            IdCliente = norm.IdCliente,
            IdBanca = idBanca,
            IdAgenzia = idAgenzia,
            IBAN = norm.IBAN,
        };

        try
        {
            await _repository.InsertAsync(entity, cancellationToken);
            await _audit.RegistraCreazioneAsync(EntityType, entity.IdBancaAppoggio, norm.BancaNome,
                AuditDettaglio.Snapshot(DettaglioAudit(norm)), cancellationToken);
            return entity.IdBancaAppoggio;
        }
        catch (SqlException ex) when (IsViolazioneVincolo(ex))
        {
            throw TraduciViolazione(ex);
        }
    }

    // ---------------------------------------------------------------------
    // Update
    // ---------------------------------------------------------------------

    public async Task AggiornaAsync(BancaAppoggioInput input, CancellationToken cancellationToken = default)
    {
        var norm = Normalizza(input);
        ValidaInput(norm);

        // Unicità IBAN: escludo l'appoggio corrente così la modifica che lascia
        // invariato il proprio IBAN non viene scambiata per un duplicato.
        await ValidaIbanUnicoAsync(norm, escludiId: norm.IdBancaAppoggio, cancellationToken);

        var precedente = await _repository.GetByIdAsync(norm.IdBancaAppoggio, cancellationToken);

        var (idBanca, idAgenzia) = await RisolviBancaAgenziaAsync(norm, cancellationToken);

        if (await _repository.ExistsLegameAttivoAsync(norm.IdCliente, idBanca, idAgenzia, escludiId: norm.IdBancaAppoggio, cancellationToken))
        {
            throw LegameDuplicato();
        }

        var entity = new BancaAppoggio
        {
            IdBancaAppoggio = norm.IdBancaAppoggio,
            IdCliente = norm.IdCliente,
            IdBanca = idBanca,
            IdAgenzia = idAgenzia,
            IBAN = norm.IBAN,
        };

        try
        {
            await _repository.UpdateAsync(entity, cancellationToken);
            var dati = precedente is null
                ? AuditDettaglio.Snapshot(DettaglioAudit(norm))
                : AuditDettaglio.Diff(DettaglioAudit(precedente), DettaglioAudit(norm));
            await _audit.RegistraModificaAsync(EntityType, norm.IdBancaAppoggio, norm.BancaNome, dati, cancellationToken);
        }
        catch (SqlException ex) when (IsViolazioneVincolo(ex))
        {
            throw TraduciViolazione(ex);
        }
    }

    // ---------------------------------------------------------------------
    // Delete (con doppia difesa)
    // ---------------------------------------------------------------------

    public async Task EliminaAsync(Guid idBancaAppoggio, CancellationToken cancellationToken = default)
    {
        if (await _repository.HasDipendenzeAsync(idBancaAppoggio, cancellationToken))
        {
            throw new BancaAppoggioConDipendenzeException(idBancaAppoggio);
        }

        var riga = await _repository.GetByIdAsync(idBancaAppoggio, cancellationToken);

        await _repository.DisattivaAsync(idBancaAppoggio, cancellationToken);
        var dati = riga is null ? null : AuditDettaglio.Snapshot(DettaglioAudit(riga));
        await _audit.RegistraEliminazioneAsync(EntityType, idBancaAppoggio, riga?.BancaNome, dati, cancellationToken);
    }

    public async Task<bool> EEliminabileAsync(Guid idBancaAppoggio, CancellationToken cancellationToken = default)
        => !await _repository.HasDipendenzeAsync(idBancaAppoggio, cancellationToken);

    // ---------------------------------------------------------------------
    // Helper privati
    // ---------------------------------------------------------------------

    // Risolve banca (obbligatoria) e agenzia (facoltativa) con get-or-create,
    // aggiornando ABI/CAB se l'utente li ha modificati.
    private async Task<(Guid IdBanca, Guid? IdAgenzia)> RisolviBancaAgenziaAsync(BancaAppoggioInput norm, CancellationToken cancellationToken)
    {
        var idBanca = await _bancaManager.RisolviAsync(norm.BancaNome, norm.ABI, cancellationToken);
        var idAgenzia = await _agenziaManager.RisolviAsync(idBanca, norm.AgenziaNome, norm.CAB, cancellationToken);
        return (idBanca, idAgenzia);
    }

    /// <summary>
    /// Normalizza l'input: trim dei nomi/codici (vuoto → null), <c>IdCliente</c>
    /// <see cref="Guid.Empty"/> → null (banca azienda), IBAN normalizzato e
    /// presente solo per la banca azienda (per il cliente forzato a null).
    /// </summary>
    private static BancaAppoggioInput Normalizza(BancaAppoggioInput i)
    {
        var idCliente = i.IdCliente == Guid.Empty ? (Guid?)null : i.IdCliente;
        var iban = BancariValidazione.NormalizzaIban(i.IBAN);

        return new BancaAppoggioInput(
            IdBancaAppoggio: i.IdBancaAppoggio,
            IdCliente: idCliente,
            BancaNome: i.BancaNome?.Trim() ?? string.Empty,
            ABI: Pulisci(i.ABI),
            AgenziaNome: Pulisci(i.AgenziaNome),
            CAB: Pulisci(i.CAB),
            // L'IBAN è presente solo sulla banca azienda (IdCliente null).
            IBAN: idCliente is null ? iban : null);
    }

    private static string? Pulisci(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>
    /// Validazione di forma + formato bancario. L'ordine segue la UX (CLAUDE.md
    /// "Ordine dei controlli in eccezioni tipizzate è UX").
    /// </summary>
    private static void ValidaInput(BancaAppoggioInput i)
    {
        if (string.IsNullOrWhiteSpace(i.BancaNome))
        {
            throw new BancaAppoggioInvalidaException(
                BancaAppoggioInvalidaMotivo.BancaObbligatoria,
                "Il nome della banca è obbligatorio.");
        }
        // Il CAB appartiene a un'agenzia: senza nome agenzia non si può attribuire.
        if (!string.IsNullOrWhiteSpace(i.CAB) && string.IsNullOrWhiteSpace(i.AgenziaNome))
        {
            throw new BancaAppoggioInvalidaException(
                BancaAppoggioInvalidaMotivo.CabSenzaAgenzia,
                "Per indicare un CAB occorre specificare l'agenzia a cui appartiene.");
        }
        if (!BancariValidazione.CodiceAbiCabFormatoValido(i.ABI))
        {
            throw new BancaAppoggioInvalidaException(
                BancaAppoggioInvalidaMotivo.AbiNonValido,
                "L'ABI deve essere di 5 cifre.");
        }
        if (!BancariValidazione.CodiceAbiCabFormatoValido(i.CAB))
        {
            throw new BancaAppoggioInvalidaException(
                BancaAppoggioInvalidaMotivo.CabNonValido,
                "Il CAB deve essere di 5 cifre.");
        }

        // IBAN obbligatorio per le banche dell'azienda: è il conto su cui si
        // ricevono i bonifici. Per le banche cliente è già forzato a null da
        // Normalizza (l'obbligo non si applica).
        if (i.IdCliente is null && string.IsNullOrWhiteSpace(i.IBAN))
        {
            throw new BancaAppoggioInvalidaException(
                BancaAppoggioInvalidaMotivo.IbanObbligatorio,
                "L'IBAN è obbligatorio per le banche dell'azienda.");
        }

        // IBAN (solo banca azienda; per il cliente è già null).
        if (!string.IsNullOrWhiteSpace(i.IBAN))
        {
            if (!BancariValidazione.IbanValido(i.IBAN))
            {
                throw new BancaAppoggioInvalidaException(
                    BancaAppoggioInvalidaMotivo.IbanNonValido,
                    "L'IBAN non è valido (controlla la sequenza di lettere e cifre).");
            }
            // Coerenza ABI/CAB ⟷ IBAN: se l'IBAN è italiano e l'utente ha
            // indicato ABI/CAB, devono coincidere con quelli contenuti nell'IBAN.
            if (BancariValidazione.TryEstraiAbiCab(i.IBAN, out var abiIban, out var cabIban))
            {
                if (!string.IsNullOrWhiteSpace(i.ABI) && !string.Equals(i.ABI, abiIban, StringComparison.Ordinal))
                {
                    throw new BancaAppoggioInvalidaException(
                        BancaAppoggioInvalidaMotivo.IbanIncoerente,
                        $"L'ABI ({i.ABI}) non coincide con quello contenuto nell'IBAN ({abiIban}).");
                }
                if (!string.IsNullOrWhiteSpace(i.CAB) && !string.Equals(i.CAB, cabIban, StringComparison.Ordinal))
                {
                    throw new BancaAppoggioInvalidaException(
                        BancaAppoggioInvalidaMotivo.IbanIncoerente,
                        $"Il CAB ({i.CAB}) non coincide con quello contenuto nell'IBAN ({cabIban}).");
                }
            }
        }
    }

    /// <summary>
    /// Garantisce l'unicità dell'IBAN tra gli appoggi attivi (vincolo
    /// applicativo, non a DB): un IBAN identifica un solo conto, quindi non può
    /// comparire su due banche di appoggio. Si applica alle sole banche azienda
    /// (per i clienti l'IBAN è già <c>null</c> dopo la normalizzazione).
    /// </summary>
    private async Task ValidaIbanUnicoAsync(BancaAppoggioInput norm, Guid? escludiId, CancellationToken cancellationToken)
    {
        if (norm.IBAN is null)
        {
            return;
        }
        if (await _repository.ExistsIbanAttivoAsync(norm.IBAN, escludiId, cancellationToken))
        {
            throw new BancaAppoggioInvalidaException(
                BancaAppoggioInvalidaMotivo.IbanDuplicato,
                "Esiste già una banca di appoggio con questo IBAN: un IBAN può appartenere a un solo conto.");
        }
    }

    private static bool IsViolazioneVincolo(SqlException ex)
        => ex.Number is SqlErrorConstraintViolation or SqlErrorDuplicateIndexKey or SqlErrorDuplicateConstraint;

    private static BancaAppoggioInvalidaException TraduciViolazione(SqlException ex)
    {
        var msg = ex.Message;
        if (msg.Contains("UX_BancheAppoggio_ClienteBancaAgenzia", StringComparison.OrdinalIgnoreCase))
        {
            return LegameDuplicato(ex);
        }
        if (msg.Contains("FK_BancheAppoggio_Cliente", StringComparison.OrdinalIgnoreCase))
        {
            return new BancaAppoggioInvalidaException(
                BancaAppoggioInvalidaMotivo.ClienteInesistente,
                "Il cliente indicato non è valido. Sceglierlo dall'elenco.", ex);
        }
        return LegameDuplicato(ex);
    }

    private static BancaAppoggioInvalidaException LegameDuplicato(Exception? inner = null)
    {
        const string m = "Esiste già una banca di appoggio con questa banca e agenzia per lo stesso intestatario.";
        return inner is null
            ? new BancaAppoggioInvalidaException(BancaAppoggioInvalidaMotivo.LegameDuplicato, m)
            : new BancaAppoggioInvalidaException(BancaAppoggioInvalidaMotivo.LegameDuplicato, m, inner);
    }

    // Oggetto leggibile per l'audit (nomi/codici risolti, niente id tecnici).
    private static object DettaglioAudit(BancaAppoggioInput i) => new
    {
        i.IdCliente,
        Banca = i.BancaNome,
        i.ABI,
        Agenzia = i.AgenziaNome,
        i.CAB,
        i.IBAN,
    };

    private static object DettaglioAudit(BancaAppoggioRiga r) => new
    {
        r.IdCliente,
        Banca = r.BancaNome,
        r.ABI,
        Agenzia = r.AgenziaNome,
        r.CAB,
        r.IBAN,
    };
}
