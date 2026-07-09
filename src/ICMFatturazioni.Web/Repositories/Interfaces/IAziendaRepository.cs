using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Repositories.Interfaces;

/// <summary>
/// Accesso ai dati per <c>fatt.Azienda</c> (dati dello studio emittente).
/// Sistema mono-studio: la lettura principale restituisce "l'azienda corrente".
/// </summary>
public interface IAziendaRepository
{
    /// <summary>
    /// Restituisce l'azienda corrente (la prima riga attiva), o <c>null</c> se
    /// non configurata.
    /// </summary>
    Task<Azienda?> GetAziendaAsync(CancellationToken ct = default);

    /// <summary>Inserisce una nuova riga cedente (Id già valorizzato app-side).</summary>
    Task InsertAsync(Azienda azienda, CancellationToken ct = default);

    /// <summary>Aggiorna la riga cedente identificata da <c>IdAzienda</c>.</summary>
    Task UpdateAsync(Azienda azienda, CancellationToken ct = default);
}
