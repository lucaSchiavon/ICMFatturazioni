namespace ICMFatturazioni.Web.Services;

/// <summary>
/// Una rata calcolata: numero progressivo, data di scadenza e importo.
/// </summary>
/// <param name="Numero">Numero progressivo della rata (1..N).</param>
/// <param name="DataScadenza">Data di scadenza calcolata.</param>
/// <param name="Importo">Importo della rata (la somma delle rate = importo totale).</param>
public sealed record RataScadenza(int Numero, DateOnly DataScadenza, decimal Importo);

/// <summary>
/// Calcolo delle date di scadenza di un pagamento (dispensa cap. 4, algoritmo
/// §5.1). Servizio puro: nessun accesso a DB, deterministico, interamente
/// testabile. Usato dall'utility "Test Date Scadenza" e, in futuro, dalla
/// schedulazione delle attività.
/// </summary>
public interface IScadenzaCalculator
{
    /// <summary>
    /// Calcola le rate dato il documento e i parametri del codice di pagamento.
    /// Per ogni rata <c>i</c>: <c>data = dataFattura + giorni[i]</c>; se
    /// <paramref name="fineMese"/> la si porta a fine mese; se in più
    /// <paramref name="ggPiu"/> &gt; 0 (solo con fine mese) si aggiungono i
    /// giorni. L'importo è ripartito in parti uguali, con l'ultima rata che
    /// assorbe il resto di arrotondamento (Σ rate = <paramref name="importo"/>).
    /// </summary>
    /// <param name="dataFattura">Data del documento.</param>
    /// <param name="numScadenze">Numero di rate (1..3).</param>
    /// <param name="giorni">Giorni alla scadenza per rata (almeno <paramref name="numScadenze"/> valori).</param>
    /// <param name="fineMese">Spostamento a fine mese.</param>
    /// <param name="ggPiu">Giorni aggiuntivi dopo il fine mese (ignorati se non fine mese).</param>
    /// <param name="importo">Importo totale da ripartire.</param>
    IReadOnlyList<RataScadenza> Calcola(
        DateOnly dataFattura,
        int numScadenze,
        IReadOnlyList<int> giorni,
        bool fineMese,
        int? ggPiu,
        decimal importo);
}
