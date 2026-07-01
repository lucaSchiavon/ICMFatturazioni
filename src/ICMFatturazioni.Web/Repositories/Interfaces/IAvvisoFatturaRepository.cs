using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Repositories.Interfaces;

/// <summary>
/// Accesso ai dati per <c>fatt.AvvisiFattura</c> e le sue righe
/// <c>fatt.AvvisoFatturaRighe</c>.
///
/// <b>Aggregate write (Opzione B, come <c>VerbaleRepository</c> di ICMVerbali).</b>
/// L'emissione e l'annullamento di un avviso toccano più tabelle e devono essere
/// atomici (pena la doppia fatturazione di una rata): sono esposti come singoli
/// metodi transazionali (<see cref="EmettiAsync"/> / <see cref="AnnullaAsync"/>)
/// che aprono una connessione + una transazione e la chiudono con commit/rollback.
/// Oltre alle due tabelle dell'avviso, questi metodi scrivono <b>solo</b> le
/// colonne-marcatore di back-reference su tabelle esterne
/// (<c>SchedulazionePagamenti.IdAvvisoRiga</c>, <c>SpeseAnticipate.IdAvviso</c>):
/// accoppiamento minimo e circoscritto all'evento "emissione avviso".
///
/// Le righe non hanno soft-delete: sono figlie dell'avviso. L'annullamento le
/// elimina (hard delete) per liberare l'indice univoco sulle scadenze e rendere
/// le rate di nuovo fatturabili.
/// </summary>
public interface IAvvisoFatturaRepository
{
    /// <summary>Restituisce gli avvisi attivi di un'attività, ordinati per DataAvviso DESC.</summary>
    Task<IReadOnlyList<AvvisoFattura>> GetByAttivitaAsync(Guid idAttivita, CancellationToken ct = default);

    /// <summary>Restituisce un avviso per chiave primaria (incluse le testate soft-deleted).</summary>
    Task<AvvisoFattura?> GetByIdAsync(Guid idAvviso, CancellationToken ct = default);

    /// <summary>Restituisce le righe di un avviso, ordinate per Ordine ASC.</summary>
    Task<IReadOnlyList<AvvisoFatturaRiga>> GetRigheByAvvisoAsync(Guid idAvviso, CancellationToken ct = default);

    /// <summary>
    /// Emissione atomica dell'avviso. In un'unica transazione: inserisce la testata,
    /// inserisce le righe, blocca le rate consumate
    /// (<c>SchedulazionePagamenti.IdAvvisoRiga = riga.IdRiga</c> per le righe con
    /// <see cref="AvvisoFatturaRiga.IdScadenza"/> valorizzata) e collega le spese
    /// anticipate (<c>SpeseAnticipate.IdAvviso</c>). Le entità arrivano con le PK
    /// già assegnate app-side (GUID v7).
    /// </summary>
    /// <exception cref="Managers.AvvisoFatturaInvalidaException">
    /// Con motivo <c>ScadenzaGiaInAvviso</c> se una rata è già stata consumata da un
    /// altro avviso (violazione dell'indice univoco): la transazione è annullata.
    /// </exception>
    Task EmettiAsync(
        AvvisoFattura testata,
        IReadOnlyList<AvvisoFatturaRiga> righe,
        IReadOnlyList<Guid> idSpeseCollegate,
        CancellationToken ct = default);

    /// <summary>
    /// Annullamento atomico dell'avviso. In un'unica transazione: sblocca le rate
    /// (azzera <c>SchedulazionePagamenti.IdAvvisoRiga</c>), scollega le spese
    /// (azzera <c>SpeseAnticipate.IdAvviso</c>), elimina le righe e soft-delete la
    /// testata (<c>IsAttivo = 0</c>). Idempotente sul piano FK: le rate tornano
    /// fatturabili.
    /// </summary>
    Task AnnullaAsync(Guid idAvviso, CancellationToken ct = default);

    /// <summary>
    /// Aggiorna i soli campi editabili della testata (data, oggetto, note, pagamento,
    /// banca, testo spese art.15) — operazione single-table. Gli snapshot fiscali
    /// (aliquote + ApplicaRitenuta) <b>non</b> vengono toccati: sono congelati
    /// all'emissione.
    /// </summary>
    Task UpdateAsync(AvvisoFattura avviso, CancellationToken ct = default);
}
