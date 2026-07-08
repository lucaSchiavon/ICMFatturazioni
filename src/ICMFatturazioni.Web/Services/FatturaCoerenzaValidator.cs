using System.Globalization;
using ICMFatturazioni.Web.Managers;
using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Web.Services;

/// <summary>
/// Implementazione di <see cref="IFatturaCoerenzaValidator"/>. Stateless → singleton.
/// </summary>
/// <remarks>
/// Basta confrontare la nuova fattura con la sua vicina di numero immediatamente
/// precedente e successiva: se ogni inserimento passa da qui, l'invariante
/// "numero crescente ⟹ data non decrescente" resta garantito sull'intero anno,
/// senza dover riscorrere tutta la sequenza. L'unicità del numero è verificata
/// anche qui (messaggio anticipato), oltre che dall'indice univoco a DB.
/// </remarks>
internal sealed class FatturaCoerenzaValidator : IFatturaCoerenzaValidator
{
    private static readonly CultureInfo _it = CultureInfo.GetCultureInfo("it-IT");

    public void Verifica(int numeroNuovo, DateOnly dataNuova, IEnumerable<FatturaNumeroData> fattureAnno)
    {
        var ordinate = fattureAnno.OrderBy(f => f.Numero).ToList();

        // 1. Unicità del numero nell'anno.
        if (ordinate.Any(f => f.Numero == numeroNuovo))
            throw new FatturaInvalidaException(
                FatturaMotivoInvalido.NumeroDuplicato,
                $"Esiste già una fattura con numero {numeroNuovo} nell'anno {dataNuova.Year}.");

        // 2. Fattura di numero immediatamente precedente e successiva.
        var precedente = ordinate
            .Where(f => f.Numero < numeroNuovo)
            .OrderByDescending(f => f.Numero)
            .FirstOrDefault();

        var successiva = ordinate
            .Where(f => f.Numero > numeroNuovo)
            .OrderBy(f => f.Numero)
            .FirstOrDefault();

        // 3. La data non può essere anteriore a quella di una fattura con numero minore.
        if (precedente is not null && dataNuova < precedente.Data)
            throw new FatturaInvalidaException(
                FatturaMotivoInvalido.SequenzaDataNumeroIncoerente,
                $"La data della fattura ({Data(dataNuova)}) è precedente alla fattura n. {precedente.Numero} " +
                $"del {Data(precedente.Data)}: la numerazione deve seguire l'ordine cronologico.");

        // 4. La data non può essere posteriore a quella di una fattura con numero maggiore.
        if (successiva is not null && dataNuova > successiva.Data)
            throw new FatturaInvalidaException(
                FatturaMotivoInvalido.SequenzaDataNumeroIncoerente,
                $"La data della fattura ({Data(dataNuova)}) è successiva alla fattura n. {successiva.Numero} " +
                $"del {Data(successiva.Data)}: la numerazione deve seguire l'ordine cronologico.");
    }

    private static string Data(DateOnly d) => d.ToString("dd/MM/yyyy", _it);
}
