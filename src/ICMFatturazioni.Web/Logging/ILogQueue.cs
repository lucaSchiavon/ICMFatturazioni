using System.Threading.Channels;
using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Logging;

/// <summary>
/// Coda in-memory che disaccoppia la produzione dei log (richieste HTTP,
/// circuiti Blazor, thread di background) dalla loro scrittura su DB. Il
/// produttore non blocca mai: sotto burst estremo gli eventi in eccesso
/// vengono scartati anziché rallentare l'app.
/// </summary>
public interface ILogQueue
{
    /// <summary>
    /// Accoda un evento senza bloccare. Ritorna <c>false</c> se la coda è piena
    /// (l'evento viene scartato): è un compromesso voluto per non rallentare le
    /// richieste a causa della diagnostica.
    /// </summary>
    bool TryEnqueue(Log entry);

    /// <summary>Lato lettura, consumato dal <c>LogWriterService</c>.</summary>
    ChannelReader<Log> Reader { get; }
}
