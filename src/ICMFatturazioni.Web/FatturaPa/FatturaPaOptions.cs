namespace ICMFatturazioni.Web.FatturaPa;

/// <summary>
/// Configurazione della generazione dei tracciati XML FatturaPA (Fase D1).
/// Bindata dalla sezione <c>FatturaPA</c> di appsettings.json.
/// </summary>
/// <remarks>
/// Il perimetro della Fase D1 è la sola <b>generazione</b> del file: l'invio allo
/// SdI avviene fuori dall'applicazione (upload manuale sul portale AdE o tramite
/// intermediario). I file prodotti vengono depositati in
/// <see cref="CartellaOutput"/>, una cartella unica del file system configurabile
/// (decisione utente 2026-07-06). Non è un segreto: è un percorso, quindi può
/// stare in appsettings versionato.
/// </remarks>
public sealed class FatturaPaOptions
{
    public const string SectionName = "FatturaPA";

    /// <summary>
    /// Cartella del file system dove salvare i tracciati XML generati (es.
    /// <c>C:\Fatturazione\XMLFattureElettroniche</c>). Se non esiste viene creata
    /// alla prima generazione. Un file per fattura, nominato secondo la convenzione
    /// FatturaPA (<c>IdPaese+IdCodiceTrasmittente_progressivo.xml</c>).
    /// </summary>
    public string CartellaOutput { get; set; } = string.Empty;
}
// NB: i codici fiscali del tracciato (TipoCassa/TipoRitenuta/CausalePagamento) NON
// stanno qui: dipendono dalla categoria del CEDENTE e sono configurati sul profilo
// fiscale di fatt.Azienda (TipoCassaFe/TipoRitenutaFe/CausalePagamentoRitenutaFe,
// migration 069), letti dal servizio XML tramite l'entità Azienda.
