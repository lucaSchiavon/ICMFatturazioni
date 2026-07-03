namespace ICMFatturazioni.Web.Models;

/// <summary>
/// Comando per creare una fattura da un avviso già emesso. Il numero è quello
/// proposto/confermato dall'utente nella maschera (default = ultimo dell'anno + 1,
/// editabile); l'anno è derivato dalla data della fattura.
/// </summary>
/// <param name="IdAvviso">Avviso da fatturare.</param>
/// <param name="NumeroFattura">Numero progressivo confermato (deve essere &gt; 0).</param>
/// <param name="DataFattura">Data della fattura (di norma odierna).</param>
public sealed record CreaFatturaRequest(
    Guid IdAvviso,
    int NumeroFattura,
    DateOnly DataFattura);
