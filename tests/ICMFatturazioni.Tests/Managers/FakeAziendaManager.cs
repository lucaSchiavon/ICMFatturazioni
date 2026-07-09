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

    // Non esercitato dai test che usano questo fake (emissione avviso): stub minimo
    // che simula il get-or-create tenendo l'ultima azienda salvata.
    public Task<Guid> SalvaCedenteAsync(Azienda input, CancellationToken ct = default)
    {
        Azienda = input;
        return Task.FromResult(input.IdAzienda == Guid.Empty ? Guid.CreateVersion7() : input.IdAzienda);
    }
}
