namespace ICMFatturazioni.Web.Authentication;

/// <summary>
/// Astrazione per l'hashing e la verifica delle password applicative.
/// L'implementazione usa PBKDF2-SHA256 tramite <c>PasswordHasher&lt;T&gt;</c>
/// di ASP.NET Core (formato v3: salt + iterazioni incluse nell'output).
/// </summary>
public interface IPasswordHasherService
{
    /// <summary>Calcola l'hash (formato v3) della password in chiaro.</summary>
    string HashPassword(string password);

    /// <summary>
    /// Verifica una password in chiaro contro l'hash memorizzato.
    /// Ritorna <c>true</c> anche quando l'hash andrebbe rigenerato (rehash),
    /// purché la password coincida.
    /// </summary>
    bool VerifyHashedPassword(string hashedPassword, string providedPassword);
}
