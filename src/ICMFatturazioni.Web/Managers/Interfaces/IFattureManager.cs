using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Web.Managers.Interfaces;

/// <summary>
/// Orchestrazione business della fattura (dispensa cap. 8), generata da un avviso.
/// La fattura non ha figli propri: eredita 1:1 righe/spese/snapshot dall'avviso. Qui
/// vivono la numerazione progressiva annuale, la validazione e l'audit (Regola 7).
/// </summary>
public interface IFattureManager
{
    /// <summary>Fattura per chiave primaria (anche annullata), o <c>null</c>.</summary>
    Task<Fattura?> GetByIdAsync(Guid idFattura, CancellationToken ct = default);

    /// <summary>
    /// Fatture emesse (attive) di un'attività, per la griglia "Stampe fatture":
    /// già arricchite con cliente/tipo/attività e ordinate per anno e numero decrescenti.
    /// </summary>
    Task<IReadOnlyList<FatturaEmessa>> ElencoEmessePerAttivitaAsync(Guid idAttivita, CancellationToken ct = default);

    /// <summary>Anni (decrescenti) per cui esiste almeno una fattura attiva, per la combo di filtro.</summary>
    Task<IReadOnlyList<int>> AnniConFattureAsync(CancellationToken ct = default);

    /// <summary>Coppie (cliente, attività) con fatture attive: restringe i selettori della maschera.</summary>
    Task<IReadOnlyList<AttivitaFatturabile>> AttivitaConFattureAsync(CancellationToken ct = default);

    /// <summary>Fattura ATTIVA di un avviso, o <c>null</c> se non ancora fatturato.</summary>
    Task<Fattura?> GetAttivaByAvvisoAsync(Guid idAvviso, CancellationToken ct = default);

    /// <summary>
    /// Numero da proporre per una nuova fattura nell'anno indicato:
    /// ultimo numero attivo dell'anno + 1 (1 se l'anno è vuoto).
    /// </summary>
    Task<int> ProponiNumeroAsync(int anno, CancellationToken ct = default);

    /// <summary>
    /// Crea la fattura da un avviso: valida (avviso esistente/attivo, non già
    /// fatturato, data e numero validi), assegna la PK GUID v7, persiste e audita.
    /// Restituisce l'Id della nuova fattura.
    /// </summary>
    /// <exception cref="FatturaInvalidaException">Se la validazione fallisce (avviso mancante/già fatturato, numero duplicato…).</exception>
    Task<Guid> CreaAsync(CreaFatturaRequest request, CancellationToken ct = default);

    /// <summary>
    /// Annulla una fattura (soft-delete): l'avviso di origine torna fatturabile e il
    /// numero è riutilizzabile. Idempotente: no-op se già annullata o inesistente.
    /// </summary>
    Task AnnullaAsync(Guid idFattura, CancellationToken ct = default);
}
