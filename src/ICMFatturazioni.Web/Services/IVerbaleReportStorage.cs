namespace ICMFatturazioni.Web.Services;

/// <summary>
/// Astrazione sull'accesso ai PDF dei verbali, prodotti e archiviati da
/// ICMVerbali. Oggi la sorgente è il <b>filesystem condiviso</b> (la cartella
/// uploads di ICMVerbali, letta in sola lettura); l'interfaccia isola questa
/// scelta così che una futura sorgente (es. endpoint HTTP di ICMVerbali) non
/// impatti Manager/UI/endpoint.
/// </summary>
/// <remarks>
/// Regola di dominio (decisa con l'utente): ICMFatturazioni <b>non genera mai</b>
/// il PDF. Mostra e scarica solo i verbali firmati il cui file esiste
/// fisicamente. Un <c>ReportPath</c> valorizzato ma senza file su disco è dato
/// sporco/legacy e va trattato come "non disponibile" (<see cref="Esiste"/> =
/// false), non rigenerato.
/// </remarks>
public interface IVerbaleReportStorage
{
    /// <summary>
    /// True se il PDF esiste fisicamente. False se <paramref name="reportPath"/>
    /// è null/vuoto, se lo storage non è configurato o se il file non c'è.
    /// </summary>
    bool Esiste(string? reportPath);

    /// <summary>
    /// Legge i byte del PDF archiviato, o <c>null</c> se il file non esiste
    /// (mai rigenerato). Non solleva per file assente: è un caso previsto.
    /// </summary>
    Task<byte[]?> LeggiAsync(string? reportPath, CancellationToken cancellationToken = default);
}
