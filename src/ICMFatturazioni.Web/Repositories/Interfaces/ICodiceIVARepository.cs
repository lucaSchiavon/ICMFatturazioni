using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Repositories.Interfaces;

/// <summary>
/// Accesso dati alla tabella <c>fatt.CodiciIVA</c>. Consumato dal solo
/// <see cref="Managers.Interfaces.ICodiceIVAManager"/>.
/// </summary>
public interface ICodiceIVARepository
{
    /// <summary>
    /// Recupera i codici IVA <b>attivi</b> (<c>IsAttivo = 1</c>) ordinati per
    /// <see cref="CodiceIVA.Codice"/>. I disattivati (soft-delete) non
    /// compaiono in elenco.
    /// </summary>
    Task<IReadOnlyList<CodiceIVA>> GetAttiviAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Recupera il codice IVA per id (anche se disattivato), o <c>null</c> se
    /// inesistente.
    /// </summary>
    Task<CodiceIVA?> GetByIdAsync(Guid idCodiceIVA, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se esiste già un codice IVA <b>attivo</b> con la stessa sigla
    /// <paramref name="codice"/> (confronto case-insensitive), escludendo
    /// l'eventuale id <paramref name="escludiId"/> (per non auto-collidere in
    /// update). Pre-check del manager per la regola di unicità.
    /// </summary>
    Task<bool> ExistsCodiceAttivoAsync(string codice, Guid? escludiId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserisce un nuovo codice IVA. L'<c>IdCodiceIVA</c> (GUID UUIDv7) è già
    /// valorizzato dal manager prima della chiamata (generazione app-side).
    /// </summary>
    Task InsertAsync(CodiceIVA codiceIva, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggiorna un codice IVA esistente. Tutti i campi sono sovrascritti: chi
    /// chiama deve passare l'oggetto completo, non un patch.
    /// </summary>
    Task UpdateAsync(CodiceIVA codiceIva, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-delete: disattiva il codice IVA (<c>IsAttivo = 0</c>), non lo
    /// rimuove fisicamente. La verifica delle dipendenze è del manager.
    /// </summary>
    Task DisattivaAsync(Guid idCodiceIVA, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se il codice IVA è referenziato da anagrafiche attive
    /// (<c>fatt.Anagrafica.IdCodiciIVA</c>). Usato dal manager per impedire la
    /// disattivazione di un codice ancora in uso e per il pattern
    /// visibility-driven della UI.
    /// </summary>
    Task<bool> HasDipendenzeAsync(Guid idCodiceIVA, CancellationToken cancellationToken = default);
}
