namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// <see cref="TimeProvider"/> pilotabile per i test: "ora" fissa e avanzabile,
/// senza attese reali. Condiviso dai test che dipendono dal calcolo di soglie
/// temporali (retention, scadenze).
/// </summary>
internal sealed class TestTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public TestTimeProvider(DateTimeOffset start) => _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan delta) => _now = _now.Add(delta);
}
