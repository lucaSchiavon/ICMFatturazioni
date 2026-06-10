using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Doppio in-memory di <see cref="IUtenteTokenRepository"/>. Replica la logica
/// di revoca-precedenti e il sentinel TOCTOU del repository reale, usando il
/// <see cref="TimeProvider"/> condiviso per valutare la scadenza (al posto di
/// SYSUTCDATETIME). Espone <see cref="PasswordImpostate"/> per verificare che il
/// consumo abbia davvero scritto la password.
/// </summary>
internal sealed class FakeUtenteTokenRepository : IUtenteTokenRepository
{
    private readonly TimeProvider _clock;
    private readonly List<UtenteToken> _tokens = new();

    public FakeUtenteTokenRepository(TimeProvider clock) => _clock = clock;

    /// <summary>Password impostate dall'ultimo consumo riuscito (utenteId → hash).</summary>
    public Dictionary<Guid, string> PasswordImpostate { get; } = new();

    public IReadOnlyList<UtenteToken> Tokens => _tokens;

    public Task CreaRevocandoPrecedentiAsync(UtenteToken nuovo, CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        foreach (var t in _tokens.Where(t =>
                     t.UtenteId == nuovo.UtenteId && t.Tipo == nuovo.Tipo
                     && t.UsatoUtc is null && t.RevocatoUtc is null))
        {
            t.RevocatoUtc = now;
        }
        _tokens.Add(nuovo);
        return Task.CompletedTask;
    }

    public Task<UtenteToken?> GetByHashAsync(byte[] tokenHash, CancellationToken cancellationToken = default)
    {
        var t = _tokens.FirstOrDefault(x => x.TokenHash.AsSpan().SequenceEqual(tokenHash));
        return Task.FromResult<UtenteToken?>(t);
    }

    public Task<int> ConsumaEImpostaPasswordAsync(Guid tokenId, Guid utenteId, string passwordHash, CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var t = _tokens.FirstOrDefault(x => x.Id == tokenId);

        // Stessa condizione del SqlMarkUsato: marca usato solo se ancora valido.
        if (t is null || t.UsatoUtc is not null || t.RevocatoUtc is not null || t.ScadenzaUtc <= now)
        {
            return Task.FromResult(0);
        }

        t.UsatoUtc = now;
        PasswordImpostate[utenteId] = passwordHash;
        return Task.FromResult(1);
    }
}
