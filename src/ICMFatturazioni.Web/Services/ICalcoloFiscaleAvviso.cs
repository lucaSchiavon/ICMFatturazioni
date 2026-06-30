namespace ICMFatturazioni.Web.Services;

/// <summary>
/// Dati di ingresso per il calcolo fiscale dell'avviso di fattura (dispensa cap. 7).
/// Le aliquote sono percentuali (es. 4 = 4%, 22 = 22%).
/// </summary>
/// <param name="Imponibile">Somma degli importi delle righe dell'avviso (prestazione).</param>
/// <param name="AliquotaCassa">Aliquota C.N.P.A.I.A. (cassa previdenza), es. 4. Parametro dello studio.</param>
/// <param name="AliquotaIva">Aliquota IVA, es. 22. Proviene dal codice IVA del cliente.</param>
/// <param name="AliquotaRitenuta">Aliquota ritenuta d'acconto, es. 20. Parametro di legge.</param>
/// <param name="ApplicaCassa">Se applicare la maggiorazione cassa (di norma sempre true per lo studio).</param>
/// <param name="ApplicaRitenuta">Se applicare la ritenuta: true solo per clienti sostituti d'imposta.</param>
/// <param name="SpeseArt15">Somma delle spese anticipate escluse ex art. 15 D.P.R. 633/72 (fuori base IVA).</param>
public sealed record CalcoloFiscaleInput(
    decimal Imponibile,
    decimal AliquotaCassa,
    decimal AliquotaIva,
    decimal AliquotaRitenuta,
    bool    ApplicaCassa,
    bool    ApplicaRitenuta,
    decimal SpeseArt15);

/// <summary>
/// Esito della cascata di calcolo fiscale dell'avviso. Tutti gli importi sono in €,
/// arrotondati a 2 decimali. Pensato per alimentare 1:1 l'anteprima/report.
/// </summary>
/// <param name="Imponibile">Totale imponibile (prestazione).</param>
/// <param name="Cassa">Maggiorazione C.N.P.A.I.A. (su imponibile).</param>
/// <param name="ImponibilePiuCassa">Imponibile + cassa (base IVA).</param>
/// <param name="Iva">IVA (su imponibile + cassa).</param>
/// <param name="Totale">Imponibile + cassa + IVA.</param>
/// <param name="Ritenuta">Ritenuta d'acconto (su imponibile); 0 se non applicata.</param>
/// <param name="SpeseArt15">Spese anticipate escluse art. 15 (aggiunte fuori IVA).</param>
/// <param name="TotaleNostroAvere">Totale − ritenuta + spese art. 15 (S.E.&amp;O.).</param>
public sealed record CalcoloFiscaleRisultato(
    decimal Imponibile,
    decimal Cassa,
    decimal ImponibilePiuCassa,
    decimal Iva,
    decimal Totale,
    decimal Ritenuta,
    decimal SpeseArt15,
    decimal TotaleNostroAvere);

/// <summary>
/// Servizio puro che applica la cascata fiscale dell'avviso di fattura
/// (imponibile → C.N.P.A.I.A. → IVA → ritenuta → spese art. 15). Stateless.
/// </summary>
public interface ICalcoloFiscaleAvviso
{
    /// <summary>Calcola la cascata fiscale a partire dagli input dell'avviso.</summary>
    CalcoloFiscaleRisultato Calcola(CalcoloFiscaleInput input);
}
