using ICMFatturazioni.Web.Entities;
using Microsoft.AspNetCore.Identity;

namespace ICMFatturazioni.Web.Authentication;

/// <summary>
/// Implementazione di <see cref="IPasswordHasherService"/> basata su
/// <see cref="PasswordHasher{TUser}"/> di ASP.NET Core (PBKDF2-HMAC-SHA256,
/// 100k iterazioni, salt 128 bit casuale, formato v3 ~130 caratteri).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="PasswordHasher{TUser}"/> è incluso nel framework ASP.NET Core
/// (shared framework Microsoft.AspNetCore.App): <b>nessun pacchetto NuGet
/// esterno</b>. Questa scelta uniforma ICMFatturazioni a ICMVerbali (ADR D22)
/// e chiude la decisione BCrypt/PBKDF2 lasciata aperta in Fase 1.
/// </para>
/// <para>
/// L'algoritmo non usa l'istanza <c>TUser</c> passata ai metodi: usiamo un
/// <see cref="Utente"/> "dummy" statico per soddisfare la firma generica.
/// </para>
/// </remarks>
internal sealed class PasswordHasherService : IPasswordHasherService
{
    private readonly PasswordHasher<Utente> _hasher = new();
    private static readonly Utente _dummy = new() { Username = "_" };

    public string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("Password obbligatoria.", nameof(password));
        }
        return _hasher.HashPassword(_dummy, password);
    }

    public bool VerifyHashedPassword(string hashedPassword, string providedPassword)
    {
        if (string.IsNullOrEmpty(hashedPassword) || string.IsNullOrEmpty(providedPassword))
        {
            return false;
        }
        var result = _hasher.VerifyHashedPassword(_dummy, hashedPassword, providedPassword);
        return result is PasswordVerificationResult.Success
                       or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
