namespace ICMFatturazioni.Web.Services;

/// <summary>
/// Genera il PDF di cortesia della Fattura, a partire dall'avviso di origine.
/// Riusa l'assemblaggio dati e il layout dell'avviso (<c>AvvisoPdfDataBuilder</c> +
/// <c>AvvisoPdfDocument</c>), variando solo titolo/barra/note/footer in modalità
/// fattura. Il documento riporta esplicitamente «NON VALIDO AI FINI FISCALI».
/// </summary>
public interface IFatturaPdfService
{
    /// <summary>
    /// Genera il PDF della fattura indicata e ne restituisce i byte.
    /// Lancia <see cref="FatturaPdfNonTrovatoException"/> se la fattura non esiste o
    /// è annullata, <see cref="AvvisoPdfNonTrovatoException"/> se l'avviso di origine
    /// è stato annullato, <see cref="AvvisoPdfDatiMancantiException"/> se mancano dati
    /// indispensabili (azienda emittente o cliente).
    /// </summary>
    Task<byte[]> GeneraAsync(Guid idFattura, CancellationToken ct = default);
}
