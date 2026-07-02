using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Web.Managers.Interfaces;

/// <summary>
/// Path "ricco" ed esplicito per registrare un errore gestito consapevolmente,
/// con una spiegazione user-friendly che il path automatico
/// (<c>DbLoggerProvider</c>) non può fornire. Lo usano i <c>catch</c> dei
/// Manager e dei componenti che gestiscono un'eccezione senza rilanciarla.
/// </summary>
/// <remarks>
/// Non rilancia <b>mai</b>: in caso di fallimento della scrittura su DB ricade
/// sul fallback (Console.Error + file). Le eccezioni tipizzate di validazione
/// (es. <c>AnagraficaInvalidaException</c>) NON vanno loggate qui: sono flusso
/// previsto, non errori (vedi Regola 6 di CLAUDE.md).
/// </remarks>
public interface ILogManager
{
    /// <summary>
    /// Registra un errore a livello <see cref="Entities.LogLivello.Error"/>.
    /// </summary>
    /// <param name="eccezione">Eccezione catturata.</param>
    /// <param name="spiegazione">
    /// Descrizione user-friendly dell'accaduto e dell'azione consigliata.
    /// Mai includere segreti (vengono comunque sanitizzati a valle).
    /// </param>
    /// <param name="sorgente">
    /// Componente/metodo che ha catturato l'errore (es. <c>Auth.ForgotPassword</c>).
    /// </param>
    /// <param name="utenteId">Id dell'utente coinvolto, se noto.</param>
    /// <param name="entityId">Id dell'entità di dominio coinvolta, se pertinente.</param>
    /// <param name="entityType">Tipo dell'entità coinvolta, se pertinente.</param>
    Task LogErroreAsync(
        Exception eccezione,
        string spiegazione,
        string sorgente,
        Guid? utenteId = null,
        Guid? entityId = null,
        string? entityType = null,
        CancellationToken cancellationToken = default);

    /// <summary>Ricerca paginata dei log (pagina di amministrazione <c>/admin/log</c>).</summary>
    Task<LogRisultato> CercaAsync(LogFiltro filtro, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tutte le righe che soddisfano il filtro (export CSV), fino a
    /// <paramref name="maxRighe"/>. Ignora la paginazione della griglia.
    /// </summary>
    Task<IReadOnlyList<Log>> EsportaAsync(LogFiltro filtro, int maxRighe, CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina i log più vecchi di <paramref name="giorni"/> giorni. Ritorna le
    /// righe eliminate. Policy di retention manuale (CLAUDE.md Regola 6).
    /// </summary>
    Task<int> PurgaPrecedentiAsync(int giorni, CancellationToken cancellationToken = default);
}
