using ICMFatturazioni.Web.Models;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Repository in-memory per testare <c>VerbaleConsultazioneManager</c> senza DB.
/// Restituisce esattamente i verbali configurati (rappresentano i "candidati"
/// già filtrati dalla vista: firmati, non cancellati, con ReportPath valorizzato).
/// </summary>
internal sealed class FakeVerbaleConsultazioneRepository : IVerbaleConsultazioneRepository
{
    public List<VerbaleConsultazione> Verbali { get; } = new();

    public Task<IReadOnlyList<VerbaleConsultazione>> GetEsportabiliAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<VerbaleConsultazione>>(Verbali.ToList());
}
