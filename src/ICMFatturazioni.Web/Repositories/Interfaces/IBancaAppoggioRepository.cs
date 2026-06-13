using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Web.Repositories.Interfaces;

/// <summary>
/// Accesso dati a <c>fatt.BancheAppoggio</c>. Le letture restituiscono il modello
/// "ricco" <see cref="BancaAppoggioRiga"/> (JOIN su <c>fatt.Banche</c>/
/// <c>fatt.Agenzie</c>); le scritture lavorano sull'entità di legame
/// <see cref="BancaAppoggio"/>. Consumato dal solo
/// <see cref="Managers.Interfaces.IBancaAppoggioManager"/>.
/// </summary>
public interface IBancaAppoggioRepository
{
    /// <summary>
    /// Appoggi attivi (azienda prima, poi clienti; per nome banca), con
    /// banca/agenzia risolte. Pronto per il <c>MudDataGrid</c>.
    /// </summary>
    Task<IReadOnlyList<BancaAppoggioRiga>> GetAttiviAsync(CancellationToken cancellationToken = default);

    /// <summary>Recupera un appoggio per id (con banca/agenzia risolte), o <c>null</c>.</summary>
    Task<BancaAppoggioRiga?> GetByIdAsync(Guid idBancaAppoggio, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appoggi selezionabili per flag banca del tipo pagamento (dispensa cap.
    /// 3.3 / 7.5): <paramref name="bancheAzienda"/> = <c>true</c> (flag A) →
    /// banche dell'Azienda; <c>false</c> (flag C) → banche del cliente
    /// <paramref name="idCliente"/>. Pronto per l'integrazione anagrafica (Tappa 6).
    /// </summary>
    Task<IReadOnlyList<BancaAppoggioRiga>> GetSelezionabiliAsync(Guid? idCliente, bool bancheAzienda, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se esiste già un appoggio attivo con lo stesso intestatario,
    /// banca e agenzia, escludendo <paramref name="escludiId"/>. Se
    /// <paramref name="idAgenzia"/> è <c>null</c> restituisce sempre <c>false</c>
    /// (il vincolo si applica solo agli appoggi con filiale indicata).
    /// </summary>
    Task<bool> ExistsLegameAttivoAsync(Guid? idCliente, Guid idBanca, Guid? idAgenzia, Guid? escludiId, CancellationToken cancellationToken = default);

    /// <summary>Inserisce un nuovo appoggio (id GUID già valorizzato dal manager).</summary>
    Task InsertAsync(BancaAppoggio banca, CancellationToken cancellationToken = default);

    /// <summary>Aggiorna un appoggio esistente.</summary>
    Task UpdateAsync(BancaAppoggio banca, CancellationToken cancellationToken = default);

    /// <summary>Soft-delete: disattiva l'appoggio (<c>IsAttivo = 0</c>).</summary>
    Task DisattivaAsync(Guid idBancaAppoggio, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se l'appoggio è referenziato da anagrafiche attive
    /// (<c>fatt.Anagrafica.IdBancaAppoggio</c>).
    /// </summary>
    Task<bool> HasDipendenzeAsync(Guid idBancaAppoggio, CancellationToken cancellationToken = default);
}
