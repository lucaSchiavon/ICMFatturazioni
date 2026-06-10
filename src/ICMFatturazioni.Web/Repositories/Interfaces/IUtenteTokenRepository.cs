using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Repositories.Interfaces;

/// <summary>
/// Accesso dati a <c>fatt.UtenteToken</c> (magic-link attivazione/reset).
/// Esposto al solo <see cref="Managers.Interfaces.IUtenteTokenManager"/>.
/// </summary>
public interface IUtenteTokenRepository
{
    /// <summary>
    /// In UNA transazione: revoca i token ancora attivi dello stesso
    /// (utente, tipo) e inserisce il nuovo. Reinvio = il vecchio link smette
    /// di funzionare.
    /// </summary>
    Task CreaRevocandoPrecedentiAsync(UtenteToken nuovo, CancellationToken cancellationToken = default);

    /// <summary>Token con quell'hash, o <c>null</c>.</summary>
    Task<UtenteToken?> GetByHashAsync(byte[] tokenHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Transazione atomica con sentinel TOCTOU: marca il token usato SOLO se
    /// ancora utilizzabile (non usato, non revocato, non scaduto) e, in tal
    /// caso, imposta la password dell'utente. Ritorna il numero di righe del
    /// mark (0 = il token non era più utilizzabile → password non toccata).
    /// </summary>
    Task<int> ConsumaEImpostaPasswordAsync(Guid tokenId, Guid utenteId, string passwordHash, CancellationToken cancellationToken = default);
}
