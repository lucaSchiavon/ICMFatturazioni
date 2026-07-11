using ICMFatturazioni.Web.Auditing;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Implementazione di <see cref="IAttivitaConsulenteManager"/>:
///   1) normalizzazione (trim della nota, vuoto → null),
///   2) validazione (FK obbligatorie, importo positivo),
///   3) guardie sui pagamenti (D-C2/D-C3): in modifica l'importo non può scendere
///      sotto il pagato e il carico non può passare a Cliente se esistono tranche;
///      l'eliminazione è bloccata (pre-check qui + sentinel SQL nel repository),
///   4) audit di ogni scrittura (Regola 7).
/// </summary>
internal sealed class AttivitaConsulenteManager : IAttivitaConsulenteManager
{
    private const string EntityType = nameof(AttivitaConsulente);

    private readonly IAttivitaConsulenteRepository _repository;
    private readonly IAuditManager _audit;

    public AttivitaConsulenteManager(IAttivitaConsulenteRepository repository, IAuditManager audit)
    {
        _repository = repository;
        _audit = audit;
    }

    public Task<IReadOnlyList<AttivitaConsulente>> ElencoPerAttivitaAsync(Guid idAttivita, CancellationToken cancellationToken = default)
        => _repository.GetByAttivitaAsync(idAttivita, cancellationToken);

    public async Task<Guid> CreaAsync(AttivitaConsulente riga, CancellationToken cancellationToken = default)
    {
        var norm = Normalizza(riga);
        ValidaCampi(norm);

        norm.IdAttivitaConsulente = Guid.CreateVersion7();
        await _repository.InsertAsync(norm, cancellationToken);
        await _audit.RegistraCreazioneAsync(EntityType, norm.IdAttivitaConsulente,
            DescrizioneAudit(norm), AuditDettaglio.Snapshot(SnapshotAudit(norm)), cancellationToken);
        return norm.IdAttivitaConsulente;
    }

    public async Task AggiornaAsync(AttivitaConsulente riga, CancellationToken cancellationToken = default)
    {
        var norm = Normalizza(riga);
        ValidaCampi(norm);

        // Guardie sui pagamenti (D-C3, residuo mai negativo): valgono solo se la
        // riga ha già tranche attive. La lettura del pagato precede l'update.
        var pagato = await _repository.GetPagatoAsync(norm.IdAttivitaConsulente, cancellationToken);
        if (pagato > 0)
        {
            if (norm.Carico != CaricoConsulenza.Studio)
                throw new AttivitaConsulenteInvalidaException(
                    AttivitaConsulenteInvalidaMotivo.CaricoConPagamenti,
                    "La consulenza ha pagamenti registrati: non può passare a carico del Cliente.");

            if (norm.Importo < pagato)
                throw new AttivitaConsulenteInvalidaException(
                    AttivitaConsulenteInvalidaMotivo.ImportoInferiorePagato,
                    $"L'importo non può essere inferiore al già pagato ({pagato:N2} €).");
        }

        var precedente = await _repository.GetByIdAsync(norm.IdAttivitaConsulente, cancellationToken);
        await _repository.UpdateAsync(norm, cancellationToken);
        var dati = precedente is null
            ? AuditDettaglio.Snapshot(SnapshotAudit(norm))
            : AuditDettaglio.Diff(SnapshotAudit(precedente), SnapshotAudit(norm));
        await _audit.RegistraModificaAsync(EntityType, norm.IdAttivitaConsulente,
            DescrizioneAudit(norm), dati, cancellationToken);
    }

    public async Task EliminaAsync(Guid idAttivitaConsulente, CancellationToken cancellationToken = default)
    {
        // D-C2, doppia difesa: pre-check qui (messaggio user-friendly) + sentinel
        // NOT EXISTS nella UPDATE del repository (correttezza sotto race condition).
        if (await _repository.HasPagamentiAsync(idAttivitaConsulente, cancellationToken))
            throw new AttivitaConsulenteConPagamentiException(idAttivitaConsulente);

        var riga = await _repository.GetByIdAsync(idAttivitaConsulente, cancellationToken);
        await _repository.DisattivaAsync(idAttivitaConsulente, cancellationToken);
        var dati = riga is null ? null : AuditDettaglio.Snapshot(SnapshotAudit(riga));
        await _audit.RegistraEliminazioneAsync(EntityType, idAttivitaConsulente,
            riga is null ? null : DescrizioneAudit(riga), dati, cancellationToken);
    }

    // -------------------------------------------------------------------------

    private static AttivitaConsulente Normalizza(AttivitaConsulente r) => new()
    {
        IdAttivitaConsulente     = r.IdAttivitaConsulente,
        IdAttivita               = r.IdAttivita,
        IdConsulente             = r.IdConsulente,
        IdTipoAttivitaConsulente = r.IdTipoAttivitaConsulente,
        Carico                   = r.Carico,
        Importo                  = r.Importo,
        // La scadenza è il promemoria di quando LO STUDIO paga il consulente
        // (dispensa cap. 3-5): sulle righe a carico del Cliente non ha senso
        // e viene azzerata qualunque cosa arrivi dalla UI.
        Scadenza                 = r.Carico == CaricoConsulenza.Studio ? r.Scadenza : null,
        Nota                     = string.IsNullOrWhiteSpace(r.Nota) ? null : r.Nota.Trim(),
        IsAttivo                 = r.IsAttivo,
        ConsulenteDescrizione             = r.ConsulenteDescrizione,
        TipoAttivitaConsulenteDescrizione = r.TipoAttivitaConsulenteDescrizione,
    };

    private static void ValidaCampi(AttivitaConsulente r)
    {
        if (r.IdAttivita == Guid.Empty)
            throw new AttivitaConsulenteInvalidaException(
                AttivitaConsulenteInvalidaMotivo.AttivitaObbligatoria,
                "Selezionare un'attività cliente.");

        if (r.IdConsulente == Guid.Empty)
            throw new AttivitaConsulenteInvalidaException(
                AttivitaConsulenteInvalidaMotivo.ConsulenteObbligatorio,
                "Il consulente è obbligatorio.");

        if (r.IdTipoAttivitaConsulente == Guid.Empty)
            throw new AttivitaConsulenteInvalidaException(
                AttivitaConsulenteInvalidaMotivo.TipoObbligatorio,
                "Il tipo attività consulente è obbligatorio.");

        if (r.Importo <= 0)
            throw new AttivitaConsulenteInvalidaException(
                AttivitaConsulenteInvalidaMotivo.ImportoNonPositivo,
                "L'importo deve essere maggiore di zero.");
    }

    // Etichetta breve per fatt.Audit: consulente (se noto dal join/UI) + importo.
    private static string DescrizioneAudit(AttivitaConsulente r)
        => $"{r.ConsulenteDescrizione ?? "Consulenza"} — {r.Importo:N2} € ({(r.Carico == CaricoConsulenza.Studio ? "Studio" : "Cliente")})";

    // Snapshot senza proprietà di navigazione (solo dati persistiti).
    private static object SnapshotAudit(AttivitaConsulente r) => new
    {
        r.IdAttivita,
        r.IdConsulente,
        r.IdTipoAttivitaConsulente,
        Carico = r.Carico.ToString(),
        r.Importo,
        r.Scadenza,
        r.Nota,
    };
}
