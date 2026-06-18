using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Managers.Interfaces;

/// <summary>
/// Orchestrazione business per i dettagli di un'attività (cap. 10.3).
/// Regole chiave:
///   • L'Ordine è assegnato dal Manager (max + 1) e non dall'utente.
///   • Una riga con <c>HasFattura = true</c> non può essere eliminata.
///   • Sposta Su/Giù scambia l'Ordine con la riga adiacente.
/// </summary>
public interface IAttivitaDettaglioManager
{
    /// <summary>Restituisce i dettagli attivi di un'attività, ordinati per Ordine ASC.</summary>
    Task<IReadOnlyList<AttivitaDettaglio>> ElencoPerAttivitaAsync(Guid idAttivita, CancellationToken ct = default);

    /// <summary>
    /// Crea un nuovo dettaglio. Assegna GUID v7 e Ordine = max + 1.
    /// Lancia <see cref="AttivitaDettaglioInvalidaException"/> se la validazione fallisce.
    /// </summary>
    Task<Guid> CreaAsync(AttivitaDettaglio dettaglio, CancellationToken ct = default);

    /// <summary>
    /// Aggiorna i campi editabili di un dettaglio esistente.
    /// Lancia <see cref="AttivitaDettaglioInvalidaException"/> se la validazione fallisce
    /// o se la riga ha <c>HasFattura = true</c>.
    /// </summary>
    Task AggiornaAsync(AttivitaDettaglio dettaglio, CancellationToken ct = default);

    /// <summary>
    /// Soft-delete del dettaglio.
    /// Lancia <see cref="AttivitaDettaglioInvalidaException"/> se la riga ha <c>HasFattura = true</c>.
    /// </summary>
    Task EliminaAsync(Guid idAttivitaDettaglio, CancellationToken ct = default);

    /// <summary>Sposta il dettaglio di una posizione verso l'alto (Ordine decrescente). No-op se già primo.</summary>
    Task SpostaSuAsync(Guid idAttivitaDettaglio, CancellationToken ct = default);

    /// <summary>Sposta il dettaglio di una posizione verso il basso (Ordine crescente). No-op se già ultimo.</summary>
    Task SpostaGiuAsync(Guid idAttivitaDettaglio, CancellationToken ct = default);
}
