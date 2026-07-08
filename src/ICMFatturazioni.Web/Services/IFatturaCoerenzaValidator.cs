using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Web.Services;

/// <summary>
/// Verifica la coerenza di numero e data di una nuova fattura rispetto alle
/// fatture già esistenti nello stesso anno. Servizio puro e stateless.
/// </summary>
/// <remarks>
/// Regola: la numerazione progressiva deve seguire l'ordine cronologico — ordinando
/// le fatture per numero, le date non devono mai decrescere. Quindi una fattura con
/// numero maggiore non può avere data anteriore a una con numero minore, e viceversa.
/// </remarks>
public interface IFatturaCoerenzaValidator
{
    /// <summary>
    /// Controlla numero/data della nuova fattura contro quelle dell'anno.
    /// </summary>
    /// <exception cref="Managers.FatturaInvalidaException">
    /// Motivo <c>NumeroDuplicato</c> se il numero è già usato nell'anno, oppure
    /// <c>SequenzaDataNumeroIncoerente</c> se la data rompe l'ordine cronologico
    /// rispetto alla fattura di numero immediatamente precedente o successiva.
    /// </exception>
    void Verifica(int numeroNuovo, DateOnly dataNuova, IEnumerable<FatturaNumeroData> fattureAnno);
}
