namespace ICMFatturazioni.Web.Services;

/// <summary>
/// Implementazione della cascata di calcolo fiscale dell'avviso (dispensa cap. 7).
/// Stateless → registrato singleton.
///
/// Ordine e basi di calcolo (verificati sugli esempi della dispensa e del report):
///   • Cassa (C.N.P.A.I.A.) = imponibile × aliquotaCassa%      → concorre alla base IVA
///   • IVA                  = (imponibile + cassa) × aliquotaIva%
///   • Totale               = imponibile + cassa + IVA
///   • Ritenuta             = imponibile × aliquotaRitenuta%    → base = solo imponibile
///                            (applicata SOLO ai clienti sostituti d'imposta)
///   • Spese art. 15        = aggiunte al totale, FUORI dalla base IVA
///   • Totale nostro avere  = Totale − ritenuta + spese art. 15
///
/// Ogni importo è arrotondato a 2 decimali (AwayFromZero), come ScadenzaCalculator.
/// </summary>
internal sealed class CalcoloFiscaleAvviso : ICalcoloFiscaleAvviso
{
    public CalcoloFiscaleRisultato Calcola(CalcoloFiscaleInput input)
    {
        if (input.Imponibile < 0)
            throw new ArgumentOutOfRangeException(nameof(input), input.Imponibile, "L'imponibile non può essere negativo.");
        if (input.SpeseArt15 < 0)
            throw new ArgumentOutOfRangeException(nameof(input), input.SpeseArt15, "Le spese art. 15 non possono essere negative.");
        if (input.AliquotaCassa < 0 || input.AliquotaIva < 0 || input.AliquotaRitenuta < 0)
            throw new ArgumentOutOfRangeException(nameof(input), "Le aliquote non possono essere negative.");

        var cassa = input.ApplicaCassa
            ? R(input.Imponibile * input.AliquotaCassa / 100m)
            : 0m;

        var imponibilePiuCassa = input.Imponibile + cassa;

        var iva = R(imponibilePiuCassa * input.AliquotaIva / 100m);

        var totale = imponibilePiuCassa + iva;

        var ritenuta = input.ApplicaRitenuta
            ? R(input.Imponibile * input.AliquotaRitenuta / 100m)
            : 0m;

        var totaleNostroAvere = totale - ritenuta + input.SpeseArt15;

        return new CalcoloFiscaleRisultato(
            Imponibile:         input.Imponibile,
            Cassa:              cassa,
            ImponibilePiuCassa: imponibilePiuCassa,
            Iva:                iva,
            Totale:             totale,
            Ritenuta:           ritenuta,
            SpeseArt15:         input.SpeseArt15,
            TotaleNostroAvere:  totaleNostroAvere);
    }

    private static decimal R(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
