using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Managers.Interfaces;

/// <summary>
/// Logica applicativa sui dati dello studio emittente/cedente
/// (<c>fatt.Azienda</c>). Sistema mono-cedente: una sola riga attiva.
/// </summary>
public interface IAziendaManager
{
    /// <summary>
    /// Restituisce l'azienda corrente (dati del cedente per l'intestazione dei
    /// documenti), o <c>null</c> se non ancora configurata.
    /// </summary>
    Task<Azienda?> GetAziendaAsync(CancellationToken ct = default);

    /// <summary>
    /// Persiste i dati del cedente. Se non esiste ancora una riga attiva la crea
    /// (creazione "pigra": nessuna riga viene scritta finché non si salva la prima
    /// volta); altromenti aggiorna quella esistente. Valida i campi (formato P.IVA,
    /// CF, CAP, email, PEC) e registra l'audit (creazione o diff).
    /// </summary>
    /// <returns>L'Id della riga cedente salvata.</returns>
    Task<Guid> SalvaCedenteAsync(Azienda input, CancellationToken ct = default);
}
