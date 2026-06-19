using ICMFatturazioni.Web.Auditing;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Implementazione di <see cref="IAttivitaManager"/>:
///   1) validazione campi obbligatori (numero, descrizione, anagrafica, tipo),
///   2) validazione coerenza date (ProgettoDefinitivo ≤ ConcessioneEdilizia ≤ InizioLavori),
///   3) doppia difesa su DELETE (HasDipendenze pre-check + soft-delete),
///   4) audit di ogni scrittura (Regola 7).
/// Nessuna normalizzazione di testo: la descrizione è libera (maiuscolo/minuscolo a scelta utente).
/// </summary>
internal sealed class AttivitaManager : IAttivitaManager
{
    private const string EntityType = nameof(Attivita);

    private readonly IAttivitaRepository _repository;
    private readonly IAuditManager _audit;

    public AttivitaManager(IAttivitaRepository repository, IAuditManager audit)
    {
        _repository = repository;
        _audit = audit;
    }

    public Task<IReadOnlyList<Attivita>> ElencoAsync(CancellationToken cancellationToken = default)
        => _repository.GetAttiviAsync(cancellationToken);

    public Task<IReadOnlyList<Attivita>> ElencoPerAnagraficaAsync(Guid idAnagrafica, CancellationToken cancellationToken = default)
        => _repository.GetByAnagraficaAsync(idAnagrafica, cancellationToken);

    public Task<IReadOnlyList<Attivita>> ElencoPerAnagraficaTipoAsync(Guid idAnagrafica, Guid idTipoAttivita, CancellationToken cancellationToken = default)
        => _repository.GetByAnagraficaETipoAsync(idAnagrafica, idTipoAttivita, cancellationToken);

    public Task<Attivita?> GetByIdAsync(Guid idAttivita, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(idAttivita, cancellationToken);

    public async Task<Guid> CreaAsync(Attivita attivita, CancellationToken cancellationToken = default)
    {
        ValidaCampi(attivita);

        var norm = Normalizza(attivita);
        norm.IdAttivita = Guid.CreateVersion7();

        await _repository.InsertAsync(norm, cancellationToken);
        await _audit.RegistraCreazioneAsync(EntityType, norm.IdAttivita,
            $"Nr.{norm.Numero} – {norm.Descrizione}",
            AuditDettaglio.Snapshot(norm), cancellationToken);

        return norm.IdAttivita;
    }

    public async Task AggiornaAsync(Attivita attivita, CancellationToken cancellationToken = default)
    {
        ValidaCampi(attivita);

        var norm      = Normalizza(attivita);
        var precedente = await _repository.GetByIdAsync(norm.IdAttivita, cancellationToken);

        await _repository.UpdateAsync(norm, cancellationToken);

        var dati = precedente is null
            ? AuditDettaglio.Snapshot(norm)
            : AuditDettaglio.Diff(precedente, norm);
        await _audit.RegistraModificaAsync(EntityType, norm.IdAttivita,
            $"Nr.{norm.Numero} – {norm.Descrizione}", dati, cancellationToken);
    }

    public async Task EliminaAsync(Guid idAttivita, CancellationToken cancellationToken = default)
    {
        if (await _repository.HasDipendenzeAsync(idAttivita, cancellationToken))
            throw new AttivitaConDipendenzeException(idAttivita);

        var attivita = await _repository.GetByIdAsync(idAttivita, cancellationToken);
        await _repository.DisattivaAsync(idAttivita, cancellationToken);

        var dati = attivita is null ? null : AuditDettaglio.Snapshot(attivita);
        await _audit.RegistraEliminazioneAsync(EntityType, idAttivita,
            attivita is null ? null : $"Nr.{attivita.Numero} – {attivita.Descrizione}",
            dati, cancellationToken);
    }

    public async Task<bool> EEliminabileAsync(Guid idAttivita, CancellationToken cancellationToken = default)
        => !await _repository.HasDipendenzeAsync(idAttivita, cancellationToken);

    // -------------------------------------------------------------------------

    private static Attivita Normalizza(Attivita a) => new()
    {
        IdAttivita          = a.IdAttivita,
        IdAnagrafica        = a.IdAnagrafica,
        IdTipoAttivita      = a.IdTipoAttivita,
        Numero              = a.Numero.Trim(),
        Descrizione         = a.Descrizione.Trim(),
        ProgettoDefinitivo  = a.ProgettoDefinitivo,
        ConcessioneEdilizia = a.ConcessioneEdilizia,
        InizioLavori        = a.InizioLavori,
        ImportoOpera        = a.ImportoOpera,
        IsAttivo            = a.IsAttivo,
    };

    private static void ValidaCampi(Attivita a)
    {
        if (string.IsNullOrWhiteSpace(a.Numero))
            throw new AttivitaInvalidaException(
                AttivitaInvalidoMotivo.NumeroNonValido,
                "Il numero/codice attività è obbligatorio.");

        if (string.IsNullOrWhiteSpace(a.Descrizione))
            throw new AttivitaInvalidaException(
                AttivitaInvalidoMotivo.DescrizioneObbligatoria,
                "La descrizione dell'attività è obbligatoria.");

        if (a.IdAnagrafica == Guid.Empty)
            throw new AttivitaInvalidaException(
                AttivitaInvalidoMotivo.AnagraficaObbligatoria,
                "Il cliente (anagrafica) è obbligatorio.");

        if (a.IdTipoAttivita == Guid.Empty)
            throw new AttivitaInvalidaException(
                AttivitaInvalidoMotivo.TipoAttivitaObbligatorio,
                "Il tipo attività è obbligatorio.");

        // Coerenza date: ProgettoDefinitivo ≤ ConcessioneEdilizia ≤ InizioLavori
        if (a.ProgettoDefinitivo.HasValue && a.ConcessioneEdilizia.HasValue
            && a.ProgettoDefinitivo > a.ConcessioneEdilizia)
            throw new AttivitaInvalidaException(
                AttivitaInvalidoMotivo.DateIncoerenti,
                "La data Progetto definitivo deve precedere la Concessione edilizia.");

        if (a.ConcessioneEdilizia.HasValue && a.InizioLavori.HasValue
            && a.ConcessioneEdilizia > a.InizioLavori)
            throw new AttivitaInvalidaException(
                AttivitaInvalidoMotivo.DateIncoerenti,
                "La data Concessione edilizia deve precedere l'Inizio lavori.");

        if (a.ProgettoDefinitivo.HasValue && a.InizioLavori.HasValue
            && a.ProgettoDefinitivo > a.InizioLavori)
            throw new AttivitaInvalidaException(
                AttivitaInvalidoMotivo.DateIncoerenti,
                "La data Progetto definitivo deve precedere l'Inizio lavori.");
    }
}
