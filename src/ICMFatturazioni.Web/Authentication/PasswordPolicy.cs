namespace ICMFatturazioni.Web.Authentication;

/// <summary>
/// Policy minima di robustezza password, condivisa da tutti i punti che
/// impostano una password (creazione utente, reset admin, set-password via
/// magic-link). Allineata a ICMVerbali (ADR D22): almeno 10 caratteri con
/// maiuscola, minuscola e cifra. Soglie conservative, da rendere configurabili
/// in un'eventuale fase ISO stretta.
/// </summary>
public static class PasswordPolicy
{
    public const int LunghezzaMinima = 10;

    public static string Requisiti =>
        $"Almeno {LunghezzaMinima} caratteri, con una maiuscola, una minuscola e una cifra.";

    /// <summary>Ritorna <c>null</c> se la password è valida, altrimenti il messaggio d'errore.</summary>
    public static string? Valida(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < LunghezzaMinima)
        {
            return $"La password deve avere almeno {LunghezzaMinima} caratteri.";
        }
        if (!password.Any(char.IsUpper))
        {
            return "La password deve contenere almeno una lettera maiuscola.";
        }
        if (!password.Any(char.IsLower))
        {
            return "La password deve contenere almeno una lettera minuscola.";
        }
        if (!password.Any(char.IsDigit))
        {
            return "La password deve contenere almeno una cifra.";
        }
        return null;
    }

    public static bool IsValida(string? password) => Valida(password) is null;
}
