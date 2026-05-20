using System.Security.Claims;
using System.Text.RegularExpressions;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;
using Microsoft.AspNetCore.Http;

namespace ICMFatturazioni.Web.Diagnostics;

/// <summary>
/// Implementazione di <see cref="IErrorLogger"/>. Singleton: stateless
/// rispetto ai dati di errore, mantiene solo il path del file di
/// fallback e un lock di sincronizzazione per la scrittura testuale.
/// </summary>
internal sealed class ErrorLogger : IErrorLogger
{
    private readonly IErrorLogRepository _repository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly string _fallbackLogPath;
    private static readonly SemaphoreSlim FallbackFileLock = new(1, 1);

    // Pattern di sanitizzazione: blanka credenziali nelle stringhe di
    // messaggi/stacktrace. Volutamente conservativo: meglio mascherare
    // troppo che lasciar trapelare un segreto. Aggiungere pattern qui se
    // emergono ulteriori formati.
    private static readonly Regex[] SanitizerPatterns =
    [
        new(@"(Password|Pwd|PasswordHash)\s*=\s*[^\s;'""]+", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"(Token|AccessToken|RefreshToken|Bearer)\s*[:=]\s*[^\s;'""]+", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"(Server|Data Source|Initial Catalog)\s*=\s*[^\s;'""]+", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ];

    public ErrorLogger(
        IErrorLogRepository repository,
        IHttpContextAccessor httpContextAccessor,
        IHostEnvironment hostEnvironment)
    {
        _repository = repository;
        _httpContextAccessor = httpContextAccessor;
        _hostEnvironment = hostEnvironment;
        _fallbackLogPath = Path.Combine(
            _hostEnvironment.ContentRootPath,
            "logs",
            "error-logger-fallback.log");
    }

    public async Task LogAsync(
        Exception ex,
        string? contesto = null,
        string? descrizioneEstesa = null,
        Severity severity = Severity.Error,
        bool handled = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ex);

        // Snapshot dei dati di contesto in un LogError immutabile.
        var entry = BuildEntry(ex, contesto, descrizioneEstesa, severity, handled);

        // Tentativo primario: scrittura su dbo.LogErrors.
        var persisted = await _repository.InsertAsync(entry, cancellationToken);
        if (persisted)
        {
            return;
        }

        // Fallback: append testuale su file. Mai propagare un'eccezione
        // dal logger — è un canale infallibile per definizione.
        try
        {
            await AppendFallbackAsync(entry, cancellationToken);
        }
        catch
        {
            // Se anche il file fallisce ci arrendiamo silenziosamente:
            // non possiamo bloccare la response per un errore di
            // diagnostica. L'evento risulterà perso, ma l'app prosegue.
        }
    }

    // -----------------------------------------------------------------
    // Costruzione dell'entry: cattura context HTTP, identity, ambiente
    // -----------------------------------------------------------------

    private LogError BuildEntry(Exception ex, string? contesto, string? descrizioneEstesa, Severity severity, bool handled)
    {
        var http = _httpContextAccessor.HttpContext;
        var user = http?.User;

        return new LogError
        {
            TimestampUtc = DateTime.UtcNow,
            ExceptionType = ex.GetType().FullName ?? ex.GetType().Name,
            Message = Sanitize(ex.Message) ?? string.Empty,
            StackTrace = Sanitize(ex.StackTrace),
            InnerExceptionType = ex.InnerException?.GetType().FullName,
            InnerExceptionMessage = Sanitize(ex.InnerException?.Message),
            InnerExceptionStackTrace = Sanitize(ex.InnerException?.StackTrace),
            Source = ex.Source,
            DescrizioneEstesa = Sanitize(descrizioneEstesa),
            Contesto = contesto,
            UserId = ParseUserId(user),
            UserName = user?.Identity?.IsAuthenticated == true ? user.Identity?.Name : null,
            RequestPath = http?.Request?.Path.Value,
            MachineName = Environment.MachineName,
            EnvironmentName = _hostEnvironment.EnvironmentName,
            CorrelationId = http?.TraceIdentifier,
            Severity = severity,
            Handled = handled,
        };
    }

    private static int? ParseUserId(ClaimsPrincipal? user)
    {
        var raw = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(raw, out var id) ? id : null;
    }

    // -----------------------------------------------------------------
    // Sanitizzazione anti-segreti
    // -----------------------------------------------------------------

    private static string? Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }
        foreach (var rx in SanitizerPatterns)
        {
            value = rx.Replace(value, m => $"{m.Groups[1].Value}=***");
        }
        return value;
    }

    // -----------------------------------------------------------------
    // Fallback su file di testo
    // -----------------------------------------------------------------

    private async Task AppendFallbackAsync(LogError entry, CancellationToken cancellationToken)
    {
        var dir = Path.GetDirectoryName(_fallbackLogPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // Formato volutamente lineare per essere grep-friendly. L'orario
        // resta UTC (coerente col DB) per evitare ambiguità su server
        // multi-fuso.
        var line = string.Join(" | ",
            entry.TimestampUtc.ToString("O"),
            $"sev={entry.Severity}",
            $"handled={entry.Handled}",
            $"ctx={entry.Contesto ?? "-"}",
            $"type={entry.ExceptionType}",
            $"msg={entry.Message?.ReplaceLineEndings(" ").Trim() ?? string.Empty}");

        await FallbackFileLock.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(_fallbackLogPath, line + Environment.NewLine, cancellationToken);
        }
        finally
        {
            FallbackFileLock.Release();
        }
    }
}
