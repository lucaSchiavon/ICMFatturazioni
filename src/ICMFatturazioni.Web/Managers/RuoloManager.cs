using ICMFatturazioni.Web.Auditing;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Implementazione di <see cref="IRuoloManager"/>. In T1 è un sottile
/// pass-through verso il repository; le validazioni e il CRUD dei ruoli
/// custom (con tutela dei ruoli di sistema) entreranno in T3.
/// </summary>
internal sealed class RuoloManager : IRuoloManager
{
    private const string EntityType = nameof(Ruolo);

    private readonly IRuoloRepository _repository;
    private readonly IAuditManager _audit;

    public RuoloManager(IRuoloRepository repository, IAuditManager audit)
    {
        _repository = repository;
        _audit = audit;
    }

    public Task<IReadOnlyList<Ruolo>> ElencoAsync(CancellationToken cancellationToken = default)
        => _repository.GetAllAsync(cancellationToken);

    public Task<Ruolo?> GetByIdAsync(Guid idRuolo, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(idRuolo, cancellationToken);

    public Task<Ruolo?> GetByCodiceAsync(string codice, CancellationToken cancellationToken = default)
        => _repository.GetByCodiceAsync(codice, cancellationToken);

    // ---------------------------------------------------------------------
    // CRUD ruoli custom (T3b)
    // ---------------------------------------------------------------------

    public async Task<Guid> CreaAsync(string nome, string? descrizione, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException("Il nome del ruolo è obbligatorio.", nameof(nome));
        }
        if (await _repository.ExistsNomeAsync(nome, null, cancellationToken))
        {
            throw new RuoloDuplicatoException(nome);
        }

        var ruolo = new Ruolo
        {
            IdRuolo = Guid.CreateVersion7(),
            Codice = null,            // custom
            Nome = nome.Trim(),
            Descrizione = descrizione,
            IsSistema = false,
            IsAttivo = true,
        };
        await _repository.InsertAsync(ruolo, cancellationToken);
        await _audit.RegistraCreazioneAsync(EntityType, ruolo.IdRuolo, ruolo.Nome,
            AuditDettaglio.Snapshot(ruolo), cancellationToken);
        return ruolo.IdRuolo;
    }

    public async Task AggiornaAsync(Guid idRuolo, string nome, string? descrizione, bool isAttivo, CancellationToken cancellationToken = default)
    {
        var esistente = await _repository.GetByIdAsync(idRuolo, cancellationToken)
            ?? throw new ArgumentException("Ruolo inesistente.", nameof(idRuolo));
        if (esistente.IsSistema)
        {
            throw new RuoloProtettoException(esistente.Nome);
        }
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException("Il nome del ruolo è obbligatorio.", nameof(nome));
        }
        if (await _repository.ExistsNomeAsync(nome, idRuolo, cancellationToken))
        {
            throw new RuoloDuplicatoException(nome);
        }
        await _repository.UpdateAsync(idRuolo, nome.Trim(), descrizione, isAttivo, cancellationToken);
        // Diff prima→dopo sui soli campi modificabili del ruolo.
        var dati = AuditDettaglio.Diff(
            new { esistente.Nome, esistente.Descrizione, esistente.IsAttivo },
            new { Nome = nome.Trim(), Descrizione = descrizione, IsAttivo = isAttivo });
        await _audit.RegistraModificaAsync(EntityType, idRuolo, nome.Trim(), dati, cancellationToken);
    }

    public async Task EliminaAsync(Guid idRuolo, CancellationToken cancellationToken = default)
    {
        var esistente = await _repository.GetByIdAsync(idRuolo, cancellationToken)
            ?? throw new ArgumentException("Ruolo inesistente.", nameof(idRuolo));
        if (esistente.IsSistema)
        {
            throw new RuoloProtettoException(esistente.Nome);
        }
        var utenti = await _repository.CountUtentiAsync(idRuolo, cancellationToken);
        if (utenti > 0)
        {
            throw new RuoloInUsoException(esistente.Nome, utenti);
        }
        await _repository.DeleteAsync(idRuolo, cancellationToken);
        await _audit.RegistraEliminazioneAsync(EntityType, idRuolo, esistente.Nome,
            AuditDettaglio.Snapshot(esistente), cancellationToken);
    }
}
