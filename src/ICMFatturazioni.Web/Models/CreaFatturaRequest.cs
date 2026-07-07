namespace ICMFatturazioni.Web.Models;

/// <summary>
/// Comando per creare una fattura da un avviso già emesso. Il numero è quello
/// proposto/confermato dall'utente nella maschera (default = ultimo dell'anno + 1,
/// editabile); l'anno è derivato dalla data della fattura.
/// </summary>
/// <param name="IdAvviso">Avviso da fatturare.</param>
/// <param name="NumeroFattura">Numero progressivo confermato (deve essere &gt; 0).</param>
/// <param name="DataFattura">Data della fattura (di norma odierna).</param>
/// <param name="Cig">
/// C.I.G. dell'appalto (opzionale, solo per enti pubblici): confluisce nel blocco
/// XML DatiOrdineAcquisto. <c>null</c>/vuoto quando non pertinente.
/// </param>
/// <param name="Cup">C.U.P. dell'investimento pubblico (opzionale, come sopra).</param>
public sealed record CreaFatturaRequest(
    Guid IdAvviso,
    int NumeroFattura,
    DateOnly DataFattura,
    string? Cig = null,
    string? Cup = null);
