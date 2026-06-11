using System.Threading.Channels;
using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Logging;

/// <summary>
/// Implementazione di <see cref="ILogQueue"/> su <see cref="Channel{T}"/>
/// bounded. Singleton: condivisa fra il <c>DbLoggerProvider</c> (produttore),
/// la rete globale di Program.cs e il <c>LogWriterService</c> (unico consumatore).
/// </summary>
internal sealed class LogQueue : ILogQueue
{
    // Capacità volutamente generosa ma finita: protegge la memoria sotto burst.
    private const int Capacita = 2048;

    private readonly Channel<Log> _channel = Channel.CreateBounded<Log>(
        new BoundedChannelOptions(Capacita)
        {
            // Quando è piena, scarta il nuovo evento invece di bloccare il
            // produttore: la diagnostica non deve mai rallentare l'app.
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,   // un solo consumatore (il BackgroundService)
            SingleWriter = false,  // più produttori concorrenti
        });

    public bool TryEnqueue(Log entry) => _channel.Writer.TryWrite(entry);

    public ChannelReader<Log> Reader => _channel.Reader;
}
