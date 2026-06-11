using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Logging;

/// <summary>
/// Implementazione di <see cref="ILogFallbackWriter"/>. Singleton: mantiene
/// solo il path del file e un lock di serializzazione. Tutte le operazioni sono
/// avvolte in try/catch: il fallback non può a sua volta far fallire il logging.
/// </summary>
internal sealed class LogFallbackWriter : ILogFallbackWriter
{
    private readonly string _fallbackLogPath;
    private static readonly SemaphoreSlim FileLock = new(1, 1);

    public LogFallbackWriter(IHostEnvironment hostEnvironment)
    {
        _fallbackLogPath = Path.Combine(hostEnvironment.ContentRootPath, "logs", "error-logger-fallback.log");
    }

    public void Write(Log entry, Exception? causa = null)
    {
        var line = Formatta(entry, causa);

        // Sink 1: stderr. Idiomatico in PaaS (raccolto dalla piattaforma) e
        // immediato in dev. Non dipende dal filesystem.
        try
        {
            Console.Error.WriteLine(line);
        }
        catch
        {
            // Niente da fare: se anche la console fallisce, resta il file.
        }

        // Sink 2: file locale. Utile on-prem/IIS dove lo stderr si perde.
        try
        {
            ScriviSuFile(line);
        }
        catch
        {
            // Ultima istanza fallita: ci si arrende in silenzio per non
            // mascherare l'errore originale né bloccare il flusso.
        }
    }

    // Formato lineare grep-friendly. Orario in UTC (coerente col DB) per non
    // ambiguare su server multi-fuso.
    private static string Formatta(Log entry, Exception? causa)
    {
        var parti = new List<string>
        {
            entry.TimestampUtc.ToString("O"),
            $"liv={entry.Livello}",
            $"src={entry.Sorgente}",
            $"type={entry.EccezioneTipo ?? "-"}",
            $"msg={entry.Messaggio.ReplaceLineEndings(" ").Trim()}",
        };
        if (causa is not null)
        {
            parti.Add($"db-error={causa.GetType().Name}: {causa.Message.ReplaceLineEndings(" ").Trim()}");
        }
        return string.Join(" | ", parti);
    }

    private void ScriviSuFile(string line)
    {
        var dir = Path.GetDirectoryName(_fallbackLogPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // Scrittura sincrona sotto lock: il writer di log è a consumatore
        // singolo, ma la rete globale (UnhandledException) può chiamare da
        // thread arbitrari, quindi serializziamo comunque.
        FileLock.Wait();
        try
        {
            File.AppendAllText(_fallbackLogPath, line + Environment.NewLine);
        }
        finally
        {
            FileLock.Release();
        }
    }
}
