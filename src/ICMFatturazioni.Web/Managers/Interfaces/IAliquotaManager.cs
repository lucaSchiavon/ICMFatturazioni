using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Managers.Interfaces;

/// <summary>
/// Aliquote fiscali correnti usate nel calcolo dell'avviso (dispensa cap. 7).
/// </summary>
/// <param name="Cnpaia">Aliquota C.N.P.A.I.A. (cassa previdenza), es. 4.</param>
/// <param name="Ritenuta">Aliquota ritenuta d'acconto, es. 20.</param>
public sealed record AliquoteFiscali(decimal Cnpaia, decimal Ritenuta);

/// <summary>
/// Orchestrazione business per le aliquote (<c>fatt.Aliquote</c>): CRUD per la
/// pagina "Aliquote vigenti" + lettura tipizzata delle aliquote di sistema
/// (CNPAIA, ritenuta) per il modulo Avviso. Ogni scrittura è tracciata in audit.
/// </summary>
public interface IAliquotaManager
{
    /// <summary>Restituisce le aliquote attive (ordinate per descrizione).</summary>
    Task<IReadOnlyList<Aliquota>> ElencoAsync(CancellationToken ct = default);

    /// <summary>Restituisce un'aliquota per Id.</summary>
    Task<Aliquota?> GetByIdAsync(Guid idAliquota, CancellationToken ct = default);

    /// <summary>
    /// Crea una nuova aliquota libera (Codice null). Assegna GUID v7.
    /// Lancia <see cref="AliquotaInvalidaException"/> se la validazione fallisce.
    /// </summary>
    Task<Guid> CreaAsync(Aliquota aliquota, CancellationToken ct = default);

    /// <summary>
    /// Aggiorna descrizione e valore di un'aliquota.
    /// Lancia <see cref="AliquotaInvalidaException"/> se la validazione fallisce.
    /// </summary>
    Task AggiornaAsync(Aliquota aliquota, CancellationToken ct = default);

    /// <summary>
    /// Soft-delete di un'aliquota libera. Lancia <see cref="AliquotaInvalidaException"/>
    /// (motivo <c>AliquotaDiSistema</c>) se si tenta di eliminare un'aliquota di sistema.
    /// </summary>
    Task EliminaAsync(Guid idAliquota, CancellationToken ct = default);

    /// <summary>
    /// Restituisce le aliquote di sistema (CNPAIA, ritenuta) per il calcolo
    /// dell'avviso. Se una non è presente si applica il default di legge/studio.
    /// </summary>
    Task<AliquoteFiscali> GetAliquoteAvvisoAsync(CancellationToken ct = default);
}
