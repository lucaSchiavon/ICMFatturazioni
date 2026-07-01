using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Models;

/// <summary>
/// Una riga dell'avviso in fase di bozza, nell'ordine deciso dall'utente. Due nature:
/// <list type="bullet">
///   <item><b>Riga reale</b>: <see cref="IdScadenza"/> valorizzato → "consuma" quella
///   rata; tipo/descrizione/importo li risolve autorevolmente il Manager dal read-model.</item>
///   <item><b>Riga descrittiva</b>: <see cref="IdScadenza"/> = null → solo testo
///   (<see cref="Descrizione"/>), senza importo.</item>
/// </list>
/// L'ordine nella lista determina l'<c>Ordine</c> delle righe: scadenze e descrittive
/// possono essere interlacciate liberamente.
/// </summary>
/// <param name="IdScadenza">Rata da fatturare; <c>null</c> per una riga descrittiva.</param>
/// <param name="Descrizione">Testo della riga descrittiva; ignorato per le righe reali.</param>
public sealed record RigaAvvisoInput(Guid? IdScadenza, string? Descrizione = null);

/// <summary>
/// Comando di emissione di un avviso di fattura, prodotto dalla UI (fase di bozza)
/// e passato al Manager. Il Manager risolve gli snapshot fiscali/pagamento e
/// costruisce righe e testata; l'<paramref name="AliquotaIva"/> è già risolta a
/// monte dalla UI (dal codice IVA del cliente) e confermata dall'utente in anteprima.
/// </summary>
/// <param name="IdAttivita">Attività fatturata.</param>
/// <param name="IdAnagrafica">Cliente destinatario.</param>
/// <param name="DataAvviso">Data dell'avviso.</param>
/// <param name="AliquotaIva">Aliquota IVA da congelare (es. 22).</param>
/// <param name="Righe">Righe dell'avviso nell'ordine scelto (scadenze + descrittive, interlacciabili).</param>
/// <param name="IdSpeseSelezionate">Spese anticipate da allegare (art. 15).</param>
/// <param name="Oggetto">Oggetto/intestazione.</param>
/// <param name="NotaSintetica">Nota breve.</param>
/// <param name="NotaTestata">Nota estesa di testata.</param>
/// <param name="IdCodicePagamento">Override condizione di pagamento; se null eredita dall'anagrafica.</param>
/// <param name="IdBancaAppoggio">Override banca d'appoggio; se null eredita dall'anagrafica.</param>
/// <param name="DescrizioneSpeseInAvviso">Testo art. 15 (obbligatorio se ci sono spese).</param>
public sealed record EmissioneAvvisoRequest(
    Guid                          IdAttivita,
    Guid                          IdAnagrafica,
    DateOnly                      DataAvviso,
    decimal                       AliquotaIva,
    IReadOnlyList<RigaAvvisoInput> Righe,
    IReadOnlyList<Guid>           IdSpeseSelezionate,
    string?                       Oggetto = null,
    string?                       NotaSintetica = null,
    string?                       NotaTestata = null,
    Guid?                         IdCodicePagamento = null,
    Guid?                         IdBancaAppoggio = null,
    string?                       DescrizioneSpeseInAvviso = null);

/// <summary>Avviso con le sue righe, per la vista di dettaglio/anteprima.</summary>
/// <param name="Testata">Testata dell'avviso.</param>
/// <param name="Righe">Righe ordinate per <c>Ordine</c>.</param>
public sealed record AvvisoDettaglio(
    AvvisoFattura Testata,
    IReadOnlyList<AvvisoFatturaRiga> Righe);
