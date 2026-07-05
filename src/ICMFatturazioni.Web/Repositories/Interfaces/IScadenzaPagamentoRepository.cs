using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Web.Repositories.Interfaces;

/// <summary>
/// Accesso ai dati per <c>fatt.SchedulazionePagamenti</c>.
/// Ordinamento implicito: <c>DataScadenza ASC</c>.
/// </summary>
public interface IScadenzaPagamentoRepository
{
    /// <summary>Restituisce le scadenze attive di un dettaglio, ordinate per DataScadenza ASC.</summary>
    Task<IReadOnlyList<ScadenzaPagamento>> GetByDettaglioAsync(Guid idAttivitaDettaglio, CancellationToken ct = default);

    /// <summary>
    /// Restituisce le scadenze <b>fatturabili</b> di un'attività: rate attive, di
    /// dettagli attivi, non ancora consumate da alcun avviso (<c>IdAvvisoRiga IS
    /// NULL</c>), arricchite con i dati del dettaglio e con quanto già allocato in
    /// avvisi attivi. Radicata sulla scadenza (join di arricchimento read-only).
    /// Ordinata per <c>Ordine</c> del dettaglio, poi <c>DataScadenza</c>.
    /// </summary>
    /// <param name="idAvvisoEscluso">
    /// Se valorizzato, esclude quell'avviso dal calcolo di
    /// <c>GiaAllocatoAvvisiPrecedenti</c>: serve in <b>modifica</b> di un avviso, dove
    /// le sue rate sono già ricaricate nella bozza e non vanno contate una seconda volta.
    /// </param>
    Task<IReadOnlyList<ScadenzaFatturabile>> GetFatturabiliByAttivitaAsync(Guid idAttivita, Guid? idAvvisoEscluso = null, CancellationToken ct = default);

    /// <summary>
    /// Restituisce le attività (con il rispettivo cliente) che hanno ancora
    /// <b>importo residuo da fatturare</b>: esiste almeno un dettaglio attivo il
    /// cui importo eccede quanto già allocato in avvisi attivi. Criterio basato
    /// sull'<b>importo</b> (non sull'esistenza di scadenze): così un dettaglio
    /// senza scadenze schedulate — che non è ancora fatturato — mantiene l'attività
    /// visibile nei filtri Avvisi (evita "buchi"). Un'attività sparisce solo quando
    /// ogni dettaglio è interamente coperto dagli avvisi.
    /// </summary>
    Task<IReadOnlyList<AttivitaFatturabile>> GetAttivitaConResiduoDaFatturareAsync(CancellationToken ct = default);

    /// <summary>
    /// Restituisce i dettagli di un'attività il cui importo non è ancora interamente
    /// schedulato in scadenze (somma rate attive &lt; importo, incluso zero scadenze),
    /// con la quota non schedulata. Alimenta la segnalazione in maschera Avvisi dei
    /// dettagli da pianificare prima di poterli fatturare.
    /// </summary>
    Task<IReadOnlyList<DettaglioDaSchedulare>> GetDettagliNonSchedulatiByAttivitaAsync(Guid idAttivita, CancellationToken ct = default);

    /// <summary>
    /// Restituisce le righe del report "Scadenziario attività clienti": tutte le
    /// scadenze attive (di dettagli/attività attivi) che soddisfano il filtro,
    /// arricchite con cliente, attività, dettaglio e stato di evasione.
    /// "Scaduta" = <c>DataScadenza &lt; oggi</c>; "evasa" = consumata da un avviso
    /// attivo (<c>IdAvvisoRiga</c> valorizzato). Ordinamento: DataScadenza,
    /// cliente, attività (il raggruppamento anno/mese lo fa il rendering).
    /// </summary>
    /// <param name="oggi">Data odierna di riferimento per il criterio scadute/non scadute.</param>
    Task<IReadOnlyList<ScadenzaReport>> GetReportScadenzarioAsync(FiltroScadenzario filtro, DateOnly oggi, CancellationToken ct = default);

    /// <summary>Restituisce una scadenza per chiave primaria (incluse soft-deleted).</summary>
    Task<ScadenzaPagamento?> GetByIdAsync(Guid idScadenza, CancellationToken ct = default);

    /// <summary>Inserisce una nuova scadenza.</summary>
    Task InsertAsync(ScadenzaPagamento scadenza, CancellationToken ct = default);

    /// <summary>Aggiorna una scadenza esistente.</summary>
    Task UpdateAsync(ScadenzaPagamento scadenza, CancellationToken ct = default);

    /// <summary>Soft-delete: imposta IsAttivo = 0.</summary>
    Task DisattivaAsync(Guid idScadenza, CancellationToken ct = default);
}
