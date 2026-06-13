using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Repositories.Interfaces;

/// <summary>
/// Accesso dati a <c>fatt.TipiPagamento</c>. Consumato dal solo
/// <see cref="Managers.Interfaces.ITipoPagamentoManager"/>.
/// </summary>
public interface ITipoPagamentoRepository
{
    /// <summary>Tipi di pagamento attivi, ordinati per descrizione.</summary>
    Task<IReadOnlyList<TipoPagamento>> GetAttiviAsync(CancellationToken cancellationToken = default);

    /// <summary>Recupera un tipo per id (anche disattivato), o <c>null</c>.</summary>
    Task<TipoPagamento?> GetByIdAsync(Guid idTipoPagamento, CancellationToken cancellationToken = default);

    /// <summary>
    /// Esiste già un tipo <b>attivo</b> con la stessa descrizione
    /// (case-insensitive), escluso <paramref name="escludiId"/>?
    /// </summary>
    Task<bool> ExistsDescrizioneAttivaAsync(string descrizione, Guid? escludiId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Esiste già un tipo <b>attivo</b> con la stessa sigla (case-insensitive),
    /// escluso <paramref name="escludiId"/>? Per sigla nulla/vuota → <c>false</c>.
    /// </summary>
    Task<bool> ExistsSiglaAttivaAsync(string? siglaPag, Guid? escludiId, CancellationToken cancellationToken = default);

    /// <summary>Inserisce un nuovo tipo (id GUID già valorizzato dal manager).</summary>
    Task InsertAsync(TipoPagamento tipo, CancellationToken cancellationToken = default);

    /// <summary>Aggiorna un tipo esistente.</summary>
    Task UpdateAsync(TipoPagamento tipo, CancellationToken cancellationToken = default);

    /// <summary>Soft-delete: disattiva il tipo (<c>IsAttivo = 0</c>).</summary>
    Task DisattivaAsync(Guid idTipoPagamento, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se il tipo è referenziato da codici di pagamento attivi (figli).
    /// </summary>
    Task<bool> HasDipendenzeAsync(Guid idTipoPagamento, CancellationToken cancellationToken = default);
}
