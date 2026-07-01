using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Models;

/// <summary>Riga puramente descrittiva inserita in un avviso (solo testo).</summary>
/// <param name="Descrizione">Testo della riga.</param>
public sealed record RigaDescrittivaInput(string Descrizione);

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
/// <param name="IdScadenzeSelezionate">Rate selezionate da fatturare (in ordine).</param>
/// <param name="IdSpeseSelezionate">Spese anticipate da allegare (art. 15).</param>
/// <param name="Oggetto">Oggetto/intestazione.</param>
/// <param name="NotaSintetica">Nota breve.</param>
/// <param name="NotaTestata">Nota estesa di testata.</param>
/// <param name="IdCodicePagamento">Override condizione di pagamento; se null eredita dall'anagrafica.</param>
/// <param name="IdBancaAppoggio">Override banca d'appoggio; se null eredita dall'anagrafica.</param>
/// <param name="DescrizioneSpeseInAvviso">Testo art. 15 (obbligatorio se ci sono spese).</param>
/// <param name="RigheDescrittive">Righe descrittive facoltative da accodare.</param>
public sealed record EmissioneAvvisoRequest(
    Guid                          IdAttivita,
    Guid                          IdAnagrafica,
    DateOnly                      DataAvviso,
    decimal                       AliquotaIva,
    IReadOnlyList<Guid>           IdScadenzeSelezionate,
    IReadOnlyList<Guid>           IdSpeseSelezionate,
    string?                       Oggetto = null,
    string?                       NotaSintetica = null,
    string?                       NotaTestata = null,
    Guid?                         IdCodicePagamento = null,
    Guid?                         IdBancaAppoggio = null,
    string?                       DescrizioneSpeseInAvviso = null,
    IReadOnlyList<RigaDescrittivaInput>? RigheDescrittive = null);

/// <summary>Avviso con le sue righe, per la vista di dettaglio/anteprima.</summary>
/// <param name="Testata">Testata dell'avviso.</param>
/// <param name="Righe">Righe ordinate per <c>Ordine</c>.</param>
public sealed record AvvisoDettaglio(
    AvvisoFattura Testata,
    IReadOnlyList<AvvisoFatturaRiga> Righe);
