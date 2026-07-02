using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Implementazione di <see cref="IAziendaManager"/>. Semplice delega di lettura:
/// non c'è ancora logica di business (né scrittura) sui dati dello studio.
/// </summary>
public sealed class AziendaManager : IAziendaManager
{
    private readonly IAziendaRepository _repo;

    public AziendaManager(IAziendaRepository repo) => _repo = repo;

    /// <inheritdoc/>
    public Task<Azienda?> GetAziendaAsync(CancellationToken ct = default)
        => _repo.GetAziendaAsync(ct);
}
