using ICMFatturazioni.Web.Auditing;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Implementazione di <see cref="IBancaManager"/>. Centralizza la logica
/// "get-or-create" dell'istituto: una banca = un nome = un ABI. Aggiornare
/// l'ABI di una banca esistente la modifica ovunque (niente doppioni con ABI
/// divergente). Audit dei soli eventi reali (Regola 7).
/// </summary>
internal sealed class BancaManager : IBancaManager
{
    private const string EntityType = nameof(Banca);

    private readonly IBancaRepository _repository;
    private readonly IAuditManager _audit;

    public BancaManager(IBancaRepository repository, IAuditManager audit)
    {
        _repository = repository;
        _audit = audit;
    }

    public Task<IReadOnlyList<Banca>> ElencoAttiveAsync(CancellationToken cancellationToken = default)
        => _repository.GetAttiveAsync(cancellationToken);

    public async Task<Guid> RisolviAsync(string nome, string? abi, CancellationToken cancellationToken = default)
    {
        nome = nome.Trim();
        abi = string.IsNullOrWhiteSpace(abi) ? null : abi.Trim();

        var esistente = await _repository.GetByNomeAttivaAsync(nome, cancellationToken);
        if (esistente is null)
        {
            var nuova = new Banca { IdBanca = Guid.CreateVersion7(), Nome = nome, ABI = abi };
            await _repository.InsertAsync(nuova, cancellationToken);
            await _audit.RegistraCreazioneAsync(EntityType, nuova.IdBanca, nuova.Nome,
                AuditDettaglio.Snapshot(nuova), cancellationToken);
            return nuova.IdBanca;
        }

        // Aggiorna l'ABI solo se l'utente ne ha fornito uno diverso (non si
        // azzera un ABI esistente lasciando il campo vuoto).
        if (abi is not null && !string.Equals(abi, esistente.ABI, StringComparison.OrdinalIgnoreCase))
        {
            var aggiornata = new Banca
            {
                IdBanca = esistente.IdBanca,
                Nome = esistente.Nome,
                ABI = abi,
                IsAttivo = esistente.IsAttivo,
            };
            await _repository.UpdateAsync(aggiornata, cancellationToken);
            await _audit.RegistraModificaAsync(EntityType, esistente.IdBanca, esistente.Nome,
                AuditDettaglio.Diff(esistente, aggiornata), cancellationToken);
        }
        return esistente.IdBanca;
    }
}
