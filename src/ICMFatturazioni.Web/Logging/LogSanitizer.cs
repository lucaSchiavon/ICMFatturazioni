using System.Text.RegularExpressions;

namespace ICMFatturazioni.Web.Logging;

/// <summary>
/// Maschera credenziali e segreti nelle stringhe di log prima della
/// persistenza (Regola 5 e Regola 6 di CLAUDE.md). Best-effort e volutamente
/// conservativo: meglio mascherare un valore innocuo che lasciar trapelare un
/// segreto. Mirror Verbali NON ha questo passaggio: è un plus mantenuto da
/// ICMFatturazioni su scelta esplicita dell'utente.
/// </summary>
/// <remarks>
/// Applicato sia sul path automatico (<c>DbLogger</c>) sia su quello esplicito
/// (<c>LogManager.LogErroreAsync</c>), così nessuna delle due vie può scrivere
/// in chiaro password, token o connection string. Aggiungere pattern qui se
/// emergono ulteriori formati.
/// </remarks>
internal static partial class LogSanitizer
{
    // Pattern con un gruppo di cattura (la "chiave") seguito dal valore da
    // oscurare. La sostituzione conserva la chiave e rimpiazza il valore con ***.
    private static readonly Regex[] Patterns =
    [
        PasswordRegex(),
        TokenRegex(),
        ConnectionStringRegex(),
    ];

    /// <summary>
    /// Restituisce la stringa con i segreti riconosciuti sostituiti da
    /// <c>chiave=***</c>. Passa attraverso <c>null</c>/vuoto invariati.
    /// </summary>
    public static string? Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }
        foreach (var rx in Patterns)
        {
            value = rx.Replace(value, static m => $"{m.Groups[1].Value}=***");
        }
        return value;
    }

    [GeneratedRegex(@"(Password|Pwd|PasswordHash)\s*=\s*[^\s;'""]+", RegexOptions.IgnoreCase)]
    private static partial Regex PasswordRegex();

    [GeneratedRegex(@"(Token|AccessToken|RefreshToken|Bearer)\s*[:=]\s*[^\s;'""]+", RegexOptions.IgnoreCase)]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"(Server|Data Source|Initial Catalog)\s*=\s*[^\s;'""]+", RegexOptions.IgnoreCase)]
    private static partial Regex ConnectionStringRegex();
}
