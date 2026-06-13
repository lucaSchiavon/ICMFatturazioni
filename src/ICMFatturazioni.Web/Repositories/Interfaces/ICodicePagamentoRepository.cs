using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Web.Repositories.Interfaces;

/// <summary>
/// Accesso dati a <c>fatt.CodiciPagamento</c>. Le letture per la griglia
/// restituiscono il modello "ricco" <see cref="CodicePagamentoRiga"/> (JOIN su
/// tipo + lookup FE); per la modifica si recupera l'entità grezza
/// <see cref="CodicePagamento"/>. Consumato dal solo
/// <see cref="Managers.Interfaces.ICodicePagamentoManager"/>.
/// </summary>
public interface ICodicePagamentoRepository
{
    /// <summary>Codici attivi (con tipo e descrizioni risolte), ordinati per descrizione.</summary>
    Task<IReadOnlyList<CodicePagamentoRiga>> GetAttiviAsync(CancellationToken cancellationToken = default);

    /// <summary>Recupera il codice (entità grezza) per id, o <c>null</c>.</summary>
    Task<CodicePagamento?> GetByIdAsync(Guid idCodicePagamento, CancellationToken cancellationToken = default);

    /// <summary>
    /// Esiste già un codice <b>attivo</b> con la stessa descrizione
    /// (case-insensitive), escluso <paramref name="escludiId"/>?
    /// </summary>
    Task<bool> ExistsDescrizioneAttivaAsync(string descrizione, Guid? escludiId, CancellationToken cancellationToken = default);

    /// <summary>Inserisce un nuovo codice (id GUID già valorizzato dal manager).</summary>
    Task InsertAsync(CodicePagamento codice, CancellationToken cancellationToken = default);

    /// <summary>Aggiorna un codice esistente.</summary>
    Task UpdateAsync(CodicePagamento codice, CancellationToken cancellationToken = default);

    /// <summary>Soft-delete: disattiva il codice (<c>IsAttivo = 0</c>).</summary>
    Task DisattivaAsync(Guid idCodicePagamento, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se il codice è referenziato da anagrafiche attive
    /// (<c>fatt.Anagrafica.IdPag</c>).
    /// </summary>
    Task<bool> HasDipendenzeAsync(Guid idCodicePagamento, CancellationToken cancellationToken = default);
}
