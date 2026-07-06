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

    /// <summary>
    /// Codice <c>TipoCassa</c> della cassa previdenziale del cedente (codelist AdE:
    /// es. <c>TC04</c> = INARCASSA per ingegneri/architetti, <c>TC02</c> = dottori
    /// commercialisti, …). Dipende dalla categoria del cedente, quindi <b>non</b> è
    /// hardcodato: va configurato solo se il cedente applica una cassa previdenziale.
    /// Una società commerciale (S.r.l.) NON ha cassa → lasciare vuoto.
    /// </summary>
    /// <remarks>
    /// La cassa finisce nel tracciato solo se il calcolo dell'avviso produce un
    /// contributo &gt; 0 (aliquota CNPAIA valorizzata). In quel caso questo codice è
    /// obbligatorio: se vuoto, la generazione XML fallisce con un messaggio chiaro.
    /// </remarks>
    public string? TipoCassa { get; set; }

    /// <summary>
    /// Codice <c>TipoRitenuta</c> del cedente (codelist AdE: <c>RT01</c> = persona
    /// fisica, <c>RT02</c> = soggetti diversi, es. studio associato/società). Serve
    /// solo se il cedente subisce ritenuta d'acconto (professionisti/lavoro
    /// autonomo). Una S.r.l. commerciale NON subisce ritenuta sulle vendite →
    /// lasciare vuoto.
    /// </summary>
    public string? TipoRitenuta { get; set; }

    /// <summary>
    /// Codice <c>CausalePagamento</c> della ritenuta (tabella modello CU/770: es.
    /// <c>A</c> = prestazioni di lavoro autonomo). Usato solo quando è presente la
    /// ritenuta. Default <c>A</c>.
    /// </summary>
    public string CausalePagamentoRitenuta { get; set; } = "A";
}
