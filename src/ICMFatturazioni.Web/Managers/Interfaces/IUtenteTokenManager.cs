using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Managers.Interfaces;

/// <summary>
/// Orchestrazione dei magic-link utente (attivazione/reset). Genera il token in
/// chiaro (restituito al chiamante per comporre l'URL), ne persiste solo
/// l'hash, valida e consuma con difesa TOCTOU.
/// </summary>
public interface IUtenteTokenManager
{
    /// <summary>Emette un link di ATTIVAZIONE (primo accesso). Ritorna il token in chiaro.</summary>
    Task<string> CreaAttivazioneAsync(Guid utenteId, CancellationToken cancellationToken = default);

    /// <summary>Emette un link di RESET password. Ritorna il token in chiaro.</summary>
    Task<string> CreaResetAsync(Guid utenteId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Valida il token grezzo per il tipo atteso. Lancia
    /// <see cref="UtenteTokenInvalidoException"/> con il motivo
    /// (NonTrovato/Revocato/GiaUsato/Scaduto) se non utilizzabile.
    /// </summary>
    Task<UtenteToken> ValidaAsync(string rawToken, UtenteTokenTipo tipoAtteso, CancellationToken cancellationToken = default);

    /// <summary>
    /// Consuma il token (uso singolo) e imposta la password atomicamente.
    /// Rivalida con sentinel TOCTOU; lancia <see cref="UtenteTokenInvalidoException"/>
    /// se nel frattempo il token è diventato inutilizzabile. Ritorna l'IdUtente.
    /// </summary>
    Task<Guid> ConsumaAsync(string rawToken, UtenteTokenTipo tipoAtteso, string passwordHash, CancellationToken cancellationToken = default);
}
