using System.Security.Cryptography;
using System.Text;
using ICMFatturazioni.Web.Authentication;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Repositories.Interfaces;
using Microsoft.Extensions.Options;

namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Implementazione di <see cref="IUtenteTokenManager"/> (mirror di ICMVerbali).
/// </summary>
/// <remarks>
/// Usa <see cref="TimeProvider"/> per leggere "adesso": rende testabili scadenza
/// e validità senza attese reali. La registrazione audit "chi-ha-emesso-cosa"
/// arriverà con lo step di mirror logging+audit (<c>fatt.Audit</c>): qui è
/// volutamente assente per non anticiparlo.
/// </remarks>
internal sealed class UtenteTokenManager : IUtenteTokenManager
{
    private readonly IUtenteTokenRepository _repository;
    private readonly TimeProvider _clock;
    private readonly UtenteTokenOptions _options;

    public UtenteTokenManager(
        IUtenteTokenRepository repository,
        TimeProvider clock,
        IOptions<UtenteTokenOptions> options)
    {
        _repository = repository;
        _clock = clock;
        _options = options.Value;
    }

    public Task<string> CreaAttivazioneAsync(Guid utenteId, CancellationToken cancellationToken = default)
        => CreaAsync(utenteId, UtenteTokenTipo.Attivazione, _options.AttivazioneOreDefault, cancellationToken);

    public Task<string> CreaResetAsync(Guid utenteId, CancellationToken cancellationToken = default)
        => CreaAsync(utenteId, UtenteTokenTipo.Reset, _options.ResetOreDefault, cancellationToken);

    private async Task<string> CreaAsync(Guid utenteId, UtenteTokenTipo tipo, int oreValidita, CancellationToken cancellationToken)
    {
        var raw = GeneraToken();
        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var entity = new UtenteToken
        {
            Id = Guid.CreateVersion7(),
            UtenteId = utenteId,
            TokenHash = CalcolaHash(raw),
            Tipo = tipo,
            ScadenzaUtc = nowUtc.AddHours(oreValidita),
            CreatedAt = nowUtc,
        };
        await _repository.CreaRevocandoPrecedentiAsync(entity, cancellationToken);
        return raw;
    }

    public async Task<UtenteToken> ValidaAsync(string rawToken, UtenteTokenTipo tipoAtteso, CancellationToken cancellationToken = default)
    {
        var entity = string.IsNullOrWhiteSpace(rawToken)
            ? null
            : await _repository.GetByHashAsync(CalcolaHash(rawToken), cancellationToken);

        // Un token del tipo sbagliato (es. link di reset aperto sulla pagina di
        // attivazione) è indistinguibile da "non trovato" per l'utente.
        if (entity is null || entity.Tipo != tipoAtteso)
        {
            throw new UtenteTokenInvalidoException(
                UtenteTokenInvalidoMotivo.NonTrovato,
                "Token non trovato o di tipo non corrispondente.");
        }

        // Ordine UX (CLAUDE.md): la revoca esplicita prevale sui motivi temporali.
        if (entity.RevocatoUtc is not null)
        {
            throw new UtenteTokenInvalidoException(
                UtenteTokenInvalidoMotivo.Revocato, "Token revocato.");
        }
        if (entity.UsatoUtc is not null)
        {
            throw new UtenteTokenInvalidoException(
                UtenteTokenInvalidoMotivo.GiaUsato, "Token già utilizzato.");
        }
        if (entity.ScadenzaUtc <= _clock.GetUtcNow().UtcDateTime)
        {
            throw new UtenteTokenInvalidoException(
                UtenteTokenInvalidoMotivo.Scaduto, "Token scaduto.");
        }

        return entity;
    }

    public async Task<Guid> ConsumaAsync(string rawToken, UtenteTokenTipo tipoAtteso, string passwordHash, CancellationToken cancellationToken = default)
    {
        // Pre-check: messaggio user-friendly se il link non è più valido.
        var entity = await ValidaAsync(rawToken, tipoAtteso, cancellationToken);

        // Use con sentinel: se lo stato cambia fra il check e l'update, il
        // repository non aggiorna righe (righe == 0) e la password non viene
        // toccata. Lo traduciamo in eccezione tipizzata coerente.
        var righe = await _repository.ConsumaEImpostaPasswordAsync(entity.Id, entity.UtenteId, passwordHash, cancellationToken);
        if (righe == 0)
        {
            throw new UtenteTokenInvalidoException(
                UtenteTokenInvalidoMotivo.GiaUsato,
                "Token non più utilizzabile (consumato o revocato).");
        }

        return entity.UtenteId;
    }

    // 32 byte casuali in Base64Url (~43 caratteri url-safe, niente padding). Il
    // token in chiaro vive solo nell'email/URL: nel DB c'è solo il suo hash.
    private static string GeneraToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] CalcolaHash(string rawToken)
        => SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
}
