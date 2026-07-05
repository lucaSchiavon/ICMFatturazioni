using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Web.Services;

/// <summary>
/// Generazione del report PDF "Scadenziario attività clienti" (maschera
/// "Stampa scadenze", modello docs/Scadenze.pdf): scadenze raggruppate per
/// anno/mese con totali di mese, anno e generale; il filtro usato è riportato
/// nel piè di pagina di ogni pagina.
/// </summary>
public interface IScadenzarioPdfService
{
    /// <summary>
    /// Genera il PDF dello scadenzario secondo il filtro. Un filtro senza
    /// corrispondenze produce comunque un PDF valido (report vuoto con
    /// messaggio), mai un'eccezione.
    /// </summary>
    Task<byte[]> GeneraAsync(FiltroScadenzario filtro, CancellationToken ct = default);
}
