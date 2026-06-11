using ICMFatturazioni.Web.Authentication;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Doppio di <see cref="ICurrentUserAccessor"/> con utente prefissato (o
/// anonimo se costruito senza argomenti).
/// </summary>
internal sealed class FakeCurrentUserAccessor : ICurrentUserAccessor
{
    private readonly (Guid?, string?) _utente;

    public FakeCurrentUserAccessor(Guid? id = null, string? nome = null) => _utente = (id, nome);

    public Task<(Guid? Id, string? Nome)> GetAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_utente);
}
