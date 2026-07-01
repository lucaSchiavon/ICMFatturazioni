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
    Task<IReadOnlyList<ScadenzaFatturabile>> GetFatturabiliByAttivitaAsync(Guid idAttivita, CancellationToken ct = default);

    /// <summary>Restituisce una scadenza per chiave primaria (incluse soft-deleted).</summary>
    Task<ScadenzaPagamento?> GetByIdAsync(Guid idScadenza, CancellationToken ct = default);

    /// <summary>Inserisce una nuova scadenza.</summary>
    Task InsertAsync(ScadenzaPagamento scadenza, CancellationToken ct = default);

    /// <summary>Aggiorna una scadenza esistente.</summary>
    Task UpdateAsync(ScadenzaPagamento scadenza, CancellationToken ct = default);

    /// <summary>Soft-delete: imposta IsAttivo = 0.</summary>
    Task DisattivaAsync(Guid idScadenza, CancellationToken ct = default);
}
