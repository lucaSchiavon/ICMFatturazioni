using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Repository in-memory per testare <c>AziendaManager</c> senza DB. Sistema
/// mono-cedente: conserva al più una riga attiva, restituita da GetAziendaAsync.
/// </summary>
internal sealed class FakeAziendaRepository : IAziendaRepository
{
    private Azienda? _corrente;

    public int InsertCount { get; private set; }
    public int UpdateCount { get; private set; }

    public Task<Azienda?> GetAziendaAsync(CancellationToken ct = default)
        => Task.FromResult(_corrente is { IsAttivo: true } ? _corrente : null);

    public Task InsertAsync(Azienda azienda, CancellationToken ct = default)
    {
        InsertCount++;
        _corrente = azienda;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Azienda azienda, CancellationToken ct = default)
    {
        UpdateCount++;
        _corrente = azienda;
        return Task.CompletedTask;
    }
}
