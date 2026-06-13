using ICMFatturazioni.Web.Auditing;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Implementazione di <see cref="IAgenziaManager"/>. Centralizza la logica
/// "get-or-create" della filiale: per una banca, un'agenzia = un nome = un CAB.
/// Aggiornare il CAB di un'agenzia esistente la modifica ovunque (niente
/// doppioni con CAB divergente — il problema segnalato dall'utente). Audit dei
/// soli eventi reali (Regola 7).
/// </summary>
internal sealed class AgenziaManager : IAgenziaManager
{
    private const string EntityType = nameof(Agenzia);

    private readonly IAgenziaRepository _repository;
    private readonly IAuditManager _audit;

    public AgenziaManager(IAgenziaRepository repository, IAuditManager audit)
    {
        _repository = repository;
        _audit = audit;
    }

    public Task<IReadOnlyList<Agenzia>> GetByBancaAsync(Guid idBanca, CancellationToken cancellationToken = default)
        => _repository.GetByBancaAttiveAsync(idBanca, cancellationToken);

    public async Task<Guid?> RisolviAsync(Guid idBanca, string? nome, string? cab, CancellationToken cancellationToken = default)
    {
        // Nessuna filiale indicata → nessuna agenzia.
        if (string.IsNullOrWhiteSpace(nome))
        {
            return null;
        }
        nome = nome.Trim();
        cab = string.IsNullOrWhiteSpace(cab) ? null : cab.Trim();

        var esistente = await _repository.GetByNomeAttivaAsync(idBanca, nome, cancellationToken);
        if (esistente is null)
        {
            var nuova = new Agenzia { IdAgenzia = Guid.CreateVersion7(), IdBanca = idBanca, Nome = nome, CAB = cab };
            await _repository.InsertAsync(nuova, cancellationToken);
            await _audit.RegistraCreazioneAsync(EntityType, nuova.IdAgenzia, nuova.Nome,
                AuditDettaglio.Snapshot(nuova), cancellationToken);
            return nuova.IdAgenzia;
        }

        // Aggiorna il CAB solo se l'utente ne ha fornito uno diverso: è il
        // comportamento "modifico il CAB → si aggiorna l'agenzia" richiesto.
        if (cab is not null && !string.Equals(cab, esistente.CAB, StringComparison.OrdinalIgnoreCase))
        {
            var aggiornata = new Agenzia
            {
                IdAgenzia = esistente.IdAgenzia,
                IdBanca = esistente.IdBanca,
                Nome = esistente.Nome,
                CAB = cab,
                IsAttivo = esistente.IsAttivo,
            };
            await _repository.UpdateAsync(aggiornata, cancellationToken);
            await _audit.RegistraModificaAsync(EntityType, esistente.IdAgenzia, esistente.Nome,
                AuditDettaglio.Diff(esistente, aggiornata), cancellationToken);
        }
        return esistente.IdAgenzia;
    }
}
