using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Doppio in-memory di <see cref="IAziendaManager"/>: restituisce l'azienda seminata
/// (o null se non configurata). Usato per pilotare il profilo fiscale del cedente
/// (cassa/ritenuta) nei test di emissione avviso.
/// </summary>
internal sealed class FakeAziendaManager : IAziendaManager
{
    public Azienda? Azienda { get; set; }

    public FakeAziendaManager(Azienda? azienda = null) => Azienda = azienda;

    public Task<Azienda?> GetAziendaAsync(CancellationToken ct = default) => Task.FromResult(Azienda);
}
