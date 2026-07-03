using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Models;
using ICMFatturazioni.Web.Services;

namespace ICMFatturazioni.Web.Managers.Interfaces;

/// <summary>
/// Orchestrazione business dell'avviso di fattura (dispensa cap. 5-7).
/// Emissione e annullamento sono operazioni atomiche delegate all'aggregate write
/// del repository; qui vivono validazione, snapshot fiscali all'emissione, calcolo
/// della cascata e audit (Regola 7, CLAUDE.md).
/// </summary>
public interface IAvvisoFatturaManager
{
    /// <summary>Elenco degli avvisi attivi di un'attività (più recenti prima).</summary>
    Task<IReadOnlyList<AvvisoFattura>> ElencoPerAttivitaAsync(Guid idAttivita, CancellationToken ct = default);

    /// <summary>
    /// Coppie (cliente, attività) con almeno un avviso attivo non ancora fatturato.
    /// Alimenta i filtri della maschera Emissione Fatture.
    /// </summary>
    Task<IReadOnlyList<AttivitaFatturabile>> AttivitaConAvvisiNonFatturatiAsync(CancellationToken ct = default);

    /// <summary>Avviso con le sue righe; <c>null</c> se inesistente.</summary>
    Task<AvvisoDettaglio?> GetDettaglioAsync(Guid idAvviso, CancellationToken ct = default);

    /// <summary>
    /// Le quattro grandezze informative ("Dettagli Attività") per i dettagli
    /// presenti nell'avviso selezionato (importo dettaglio, altri avvisi, avviso
    /// attuale, residuo).
    /// </summary>
    Task<IReadOnlyList<DettaglioAvvisoGrandezze>> DettagliGrandezzeAsync(Guid idAvviso, CancellationToken ct = default);

    /// <summary>
    /// Scadenze ancora fatturabili di un'attività, per la griglia di selezione
    /// dell'avviso (raggruppate per dettaglio, quanto già allocato incluso).
    /// In <b>modifica</b> passare <paramref name="idAvvisoEscluso"/> per non contare
    /// due volte le rate dell'avviso in corso di modifica (già ricaricate in bozza).
    /// </summary>
    Task<IReadOnlyList<ScadenzaFatturabile>> ScadenzeFatturabiliAsync(Guid idAttivita, Guid? idAvvisoEscluso = null, CancellationToken ct = default);

    /// <summary>
    /// Attività (con il rispettivo cliente) ancora fatturabili, cioè con importo
    /// residuo non ancora messo in avviso (criterio sull'importo, non sull'esistenza
    /// di scadenze). Alimenta i filtri della maschera: un'attività sparisce solo
    /// quando ogni dettaglio è interamente coperto dagli avvisi.
    /// </summary>
    Task<IReadOnlyList<AttivitaFatturabile>> AttivitaFatturabiliAsync(CancellationToken ct = default);

    /// <summary>
    /// Dettagli dell'attività non ancora interamente schedulati in scadenze: non
    /// sono fatturabili finché non si pianificano le rate. Serve alla maschera per
    /// segnalare gli importi "da schedulare" ed evitare buchi non fatturati.
    /// </summary>
    Task<IReadOnlyList<DettaglioDaSchedulare>> DettagliDaSchedulareAsync(Guid idAttivita, CancellationToken ct = default);

    /// <summary>
    /// Emette un avviso in modo atomico: valida il comando, risolve gli snapshot
    /// fiscali (aliquote di sistema, IVA, ritenuta dal sostituto d'imposta) e di
    /// pagamento (dall'anagrafica, salvo override), costruisce le righe con importi
    /// autorevoli dalle scadenze selezionate, blocca le rate e collega le spese.
    /// Restituisce l'Id del nuovo avviso.
    /// </summary>
    /// <exception cref="AvvisoFatturaInvalidaException">Se la validazione fallisce o una rata non è più fatturabile.</exception>
    Task<Guid> EmettiAsync(EmissioneAvvisoRequest request, CancellationToken ct = default);

    /// <summary>
    /// Annulla un avviso in modo atomico (sblocca rate, scollega spese, elimina
    /// righe, soft-delete testata). Idempotente: no-op se già annullato o inesistente.
    /// </summary>
    Task AnnullaAsync(Guid idAvviso, CancellationToken ct = default);

    /// <summary>
    /// Aggiorna i campi editabili della testata (data, oggetto, note, pagamento,
    /// banca, testo spese art.15). Gli snapshot fiscali non sono modificabili.
    /// </summary>
    /// <exception cref="AvvisoFatturaInvalidaException">Se la validazione fallisce o l'avviso è già fatturato.</exception>
    Task AggiornaTestataAsync(AvvisoFattura avviso, CancellationToken ct = default);

    /// <summary>
    /// Modifica i dettagli di un avviso esistente (stessa interazione della fase-2 di
    /// emissione): riordino, testo delle righe, aggiunta/rimozione di righe descrittive,
    /// aggiunta di NUOVE rate dal pool fatturabili e rimozione di rate (che tornano
    /// fatturabili), più la riconciliazione delle spese collegate. Le righe reali
    /// mantengono importo/tipo/dettaglio autorevoli (dalla riga originale se mantenuta,
    /// dal read-model fatturabile se aggiunta); l'operazione è atomica (replace-all).
    /// </summary>
    /// <exception cref="AvvisoFatturaInvalidaException">
    /// Se l'avviso non esiste, è già fatturato, una rata non è fatturabile, o il
    /// risultato non avrebbe né rate né spese (avviso vuoto).
    /// </exception>
    Task AggiornaDettagliAsync(
        Guid idAvviso,
        IReadOnlyList<ModificaRigaAvvisoInput> righe,
        IReadOnlyList<Guid> idSpeseSelezionate,
        CancellationToken ct = default);

    /// <summary>
    /// Applica la cascata fiscale usando gli snapshot congelati sull'avviso. Adatta
    /// la testata (aliquote + applica-ritenuta) e i totali forniti dal chiamante
    /// (imponibile = somma righe, spese art.15 = somma spese collegate) in un
    /// <see cref="CalcoloFiscaleRisultato"/> per l'anteprima/report. Puro.
    /// </summary>
    CalcoloFiscaleRisultato Calcola(AvvisoFattura avviso, decimal imponibile, decimal speseArt15);
}
