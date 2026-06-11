using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Logging;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Doppio in-memory di <see cref="ILogFallbackWriter"/>: registra le righe
/// riversate sul fallback (senza toccare console o filesystem nei test).
/// </summary>
internal sealed class FakeLogFallbackWriter : ILogFallbackWriter
{
    public List<(Log Entry, Exception? Causa)> Scritture { get; } = new();

    public void Write(Log entry, Exception? causa = null) => Scritture.Add((entry, causa));
}
