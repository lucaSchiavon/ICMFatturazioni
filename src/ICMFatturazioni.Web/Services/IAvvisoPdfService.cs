namespace ICMFatturazioni.Web.Services;

/// <summary>
/// Genera il PDF di cortesia dell'Avviso di fattura ("avviso di parcella").
/// Mirror architetturale di <c>IVerbalePdfService</c> in ICMVerbali: carica i dati
/// via i Manager, li impacchetta in un DTO immutabile e delega il rendering
/// sincrono a <c>AvvisoPdfDocument</c> (PDFsharp-MigraDoc).
/// </summary>
public interface IAvvisoPdfService
{
    /// <summary>
    /// Genera il PDF dell'avviso indicato e ne restituisce i byte.
    /// Lancia <see cref="AvvisoPdfNonTrovatoException"/> se l'avviso non esiste o
    /// è stato annullato, <see cref="AvvisoPdfDatiMancantiException"/> se mancano
    /// dati indispensabili (azienda emittente o cliente).
    /// </summary>
    Task<byte[]> GeneraAsync(Guid idAvviso, CancellationToken ct = default);
}
