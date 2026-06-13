namespace ICMFatturazioni.Web.Services;

/// <summary>
/// Implementazione dell'algoritmo di calcolo scadenza (dispensa §5.1).
/// Stateless → registrato singleton.
/// </summary>
internal sealed class ScadenzaCalculator : IScadenzaCalculator
{
    public IReadOnlyList<RataScadenza> Calcola(
        DateOnly dataFattura,
        int numScadenze,
        IReadOnlyList<int> giorni,
        bool fineMese,
        int? ggPiu,
        decimal importo)
    {
        if (numScadenze is < 1 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(numScadenze), numScadenze, "Le scadenze devono essere tra 1 e 3.");
        }
        if (giorni.Count < numScadenze)
        {
            throw new ArgumentException("Servono almeno tanti valori di giorni quante sono le scadenze.", nameof(giorni));
        }

        // Ripartizione importo: parti uguali arrotondate a 2 decimali; l'ultima
        // rata assorbe il resto così che Σ rate == importo.
        var importoRata = Math.Round(importo / numScadenze, 2, MidpointRounding.AwayFromZero);

        var rate = new List<RataScadenza>(numScadenze);
        decimal accumulato = 0m;
        for (var i = 0; i < numScadenze; i++)
        {
            // 1) somma dei giorni di calendario.
            var data = dataFattura.AddDays(giorni[i]);
            // 2) spostamento a fine mese.
            if (fineMese)
            {
                data = UltimoGiornoDelMese(data);
                // 3) giorni aggiuntivi (solo dopo il fine mese).
                if (ggPiu is > 0)
                {
                    data = data.AddDays(ggPiu.Value);
                }
            }

            var ultima = i == numScadenze - 1;
            var importoCorrente = ultima ? importo - accumulato : importoRata;
            accumulato += importoCorrente;

            rate.Add(new RataScadenza(i + 1, data, importoCorrente));
        }

        return rate;
    }

    private static DateOnly UltimoGiornoDelMese(DateOnly d)
        => new(d.Year, d.Month, DateTime.DaysInMonth(d.Year, d.Month));
}
