using ICMFatturazioni.Web.Services;
using Microsoft.Extensions.Options;

namespace ICMFatturazioni.Web.Services;

/// <summary>
/// Opzioni di configurazione dello storage dei report verbali
/// (sezione <c>VerbaliReport</c> di appsettings).
/// </summary>
public sealed class VerbaliReportOptions
{
    public const string SectionName = "VerbaliReport";

    /// <summary>
    /// Percorso assoluto della cartella <c>uploads</c> di ICMVerbali (quella che
    /// contiene la sottocartella <c>report</c>). L'<c>App Pool</c> di
    /// ICMFatturazioni deve avere permesso di <b>sola lettura</b> su questa
    /// cartella. Vuoto = storage non configurato (nessun verbale sarà scaricabile).
    /// </summary>
    public string BasePath { get; set; } = string.Empty;
}

/// <summary>
/// Implementazione filesystem di <see cref="IVerbaleReportStorage"/>: risolve il
/// <c>ReportPath</c> relativo (es. <c>report/{guid}.pdf</c>) sotto
/// <see cref="VerbaliReportOptions.BasePath"/> e legge il file in sola lettura.
/// </summary>
/// <remarks>
/// Difesa contro il path traversal: il percorso risolto deve restare dentro
/// <c>BasePath</c> (il <c>ReportPath</c> arriva dal DB, quindi è affidabile, ma
/// la verifica è a costo nullo e previene sorprese in caso di dati anomali).
/// </remarks>
internal sealed class VerbaleReportStorage : IVerbaleReportStorage
{
    private readonly string _basePathAssoluto;
    private readonly ILogger<VerbaleReportStorage> _logger;

    public VerbaleReportStorage(IOptions<VerbaliReportOptions> options, ILogger<VerbaleReportStorage> logger)
    {
        _logger = logger;
        var raw = options.Value.BasePath?.Trim() ?? string.Empty;
        // Normalizza in percorso assoluto canonico una volta sola (stateless a runtime).
        _basePathAssoluto = string.IsNullOrEmpty(raw)
            ? string.Empty
            : Path.GetFullPath(raw);
    }

    public bool Esiste(string? reportPath)
    {
        var full = RisolviPathSicuro(reportPath);
        return full is not null && File.Exists(full);
    }

    public async Task<byte[]?> LeggiAsync(string? reportPath, CancellationToken cancellationToken = default)
    {
        var full = RisolviPathSicuro(reportPath);
        if (full is null || !File.Exists(full))
        {
            return null;
        }
        return await File.ReadAllBytesAsync(full, cancellationToken);
    }

    /// <summary>
    /// Combina BasePath + reportPath e restituisce il percorso assoluto solo se
    /// è valido e contenuto in BasePath; altrimenti null (storage non
    /// configurato, path vuoto o tentativo di uscire dalla cartella).
    /// </summary>
    private string? RisolviPathSicuro(string? reportPath)
    {
        if (string.IsNullOrEmpty(_basePathAssoluto) || string.IsNullOrWhiteSpace(reportPath))
        {
            return null;
        }

        // ReportPath usa lo '/' (stile URL); su Windows Path lo gestisce, ma
        // normalizziamo esplicitamente per robustezza cross-OS.
        var relativo = reportPath.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        var combinato = Path.GetFullPath(Path.Combine(_basePathAssoluto, relativo));

        // Il file deve restare sotto BasePath (anti path-traversal).
        var radice = _basePathAssoluto.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!combinato.StartsWith(radice, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("ReportPath fuori da BasePath ignorato: {ReportPath}", reportPath);
            return null;
        }
        return combinato;
    }
}
