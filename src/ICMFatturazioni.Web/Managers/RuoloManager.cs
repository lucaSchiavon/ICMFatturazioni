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
    private readonly IRuoloRepository _repository;

    public RuoloManager(IRuoloRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<Ruolo>> ElencoAsync(CancellationToken cancellationToken = default)
        => _repository.GetAllAsync(cancellationToken);

    public Task<Ruolo?> GetByIdAsync(Guid idRuolo, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(idRuolo, cancellationToken);

    public Task<Ruolo?> GetByCodiceAsync(string codice, CancellationToken cancellationToken = default)
        => _repository.GetByCodiceAsync(codice, cancellationToken);
}
