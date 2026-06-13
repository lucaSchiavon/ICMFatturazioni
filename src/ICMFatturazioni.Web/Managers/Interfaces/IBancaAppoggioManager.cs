using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Web.Managers.Interfaces;

/// <summary>
/// Logica applicativa sul catalogo Banche di appoggio. Orchestrazione del
/// modello normalizzato: risolve banca/agenzia (get-or-create) e scrive il
/// legame. Tutte le operazioni di UI passano da qui.
/// </summary>
public interface IBancaAppoggioManager
{
    /// <summary>
    /// Elenco degli appoggi attivi (con banca/agenzia risolte), azienda prima.
    /// Pronto per il <c>MudDataGrid</c>.
    /// </summary>
    Task<IReadOnlyList<BancaAppoggioRiga>> ElencoAsync(CancellationToken cancellationToken = default);

    /// <summary>Recupera un appoggio per id (con banca/agenzia risolte), o <c>null</c>.</summary>
    Task<BancaAppoggioRiga?> GetByIdAsync(Guid idBancaAppoggio, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appoggi selezionabili per flag banca del tipo pagamento (dispensa cap.
    /// 3.3 / 7.5): <paramref name="bancheAzienda"/> = <c>true</c> (flag A) →
    /// banche dell'Azienda; <c>false</c> (flag C) → banche del cliente.
    /// Predisposto per l'integrazione anagrafica (Tappa 6).
    /// </summary>
    Task<IReadOnlyList<BancaAppoggioRiga>> SelezionabiliAsync(Guid? idCliente, bool bancheAzienda, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea un nuovo appoggio dai dati del form. Risolve banca/agenzia
    /// (get-or-create, aggiornando ABI/CAB se modificati), valida (Banca
    /// obbligatoria, formato ABI/CAB, IBAN formato+checksum+coerenza con ABI/CAB)
    /// e rilancia <see cref="BancaAppoggioInvalidaException"/> con motivo
    /// specifico. Ritorna l'<c>IdBancaAppoggio</c> assegnato.
    /// </summary>
    Task<Guid> CreaAsync(BancaAppoggioInput input, CancellationToken cancellationToken = default);

    /// <summary>Aggiorna un appoggio esistente. Stesse validazioni di <see cref="CreaAsync"/>.</summary>
    Task AggiornaAsync(BancaAppoggioInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina (soft-delete) un appoggio. Solleva
    /// <see cref="BancaAppoggioConDipendenzeException"/> se ancora usato da
    /// anagrafiche attive.
    /// </summary>
    Task EliminaAsync(Guid idBancaAppoggio, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se l'appoggio è eliminabile (cioè se NON ha dipendenze). Usata
    /// dalla UI per il pattern visibility-driven del pulsante "Elimina".
    /// </summary>
    Task<bool> EEliminabileAsync(Guid idBancaAppoggio, CancellationToken cancellationToken = default);
}
