using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Managers.Interfaces;

/// <summary>
/// Logica applicativa sui ruoli. In T1 sono sole letture (servono ad
/// autenticazione e seed); creazione/modifica dei ruoli custom arriveranno
/// con la UI di amministrazione (T3).
/// </summary>
public interface IRuoloManager
{
    Task<IReadOnlyList<Ruolo>> ElencoAsync(CancellationToken cancellationToken = default);
    Task<Ruolo?> GetByIdAsync(Guid idRuolo, CancellationToken cancellationToken = default);
    Task<Ruolo?> GetByCodiceAsync(string codice, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea un nuovo ruolo custom. Lancia <see cref="RuoloDuplicatoException"/>
    /// se il nome esiste già. Ritorna l'IdRuolo (GUID v7) generato.
    /// </summary>
    Task<Guid> CreaAsync(string nome, string? descrizione, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggiorna un ruolo custom. Lancia <see cref="RuoloProtettoException"/> sui
    /// ruoli di sistema e <see cref="RuoloDuplicatoException"/> sul nome duplicato.
    /// </summary>
    Task AggiornaAsync(Guid idRuolo, string nome, string? descrizione, bool isAttivo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina un ruolo custom. Lancia <see cref="RuoloProtettoException"/> sui
    /// ruoli di sistema e <see cref="RuoloInUsoException"/> se assegnato a utenti.
    /// </summary>
    Task EliminaAsync(Guid idRuolo, CancellationToken cancellationToken = default);
}
