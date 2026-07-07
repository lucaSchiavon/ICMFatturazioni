using System.Text;
using System.Xml;

using FatturaElettronica.Extensions;

using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.FatturaPa;
using ICMFatturazioni.Web.Services;

namespace ICMFatturazioni.Tests.Services;

/// <summary>
/// Test della mappatura al tracciato FatturaPA (<c>FatturaPaXmlService.Mappa</c>):
/// il punto più delicato della Fase D1. Verifica che un avviso realistico (studio di
/// architetti con cassa INARCASSA, ritenuta d'acconto, spese escluse art. 15) produca
/// un tracciato che supera la <b>validazione offline</b> della libreria e contenga i
/// valori attesi. Copre anche la codifica del progressivo invio.
/// </summary>
public class FatturaPaXmlServiceTests
{
    // Due P.IVA italiane con check digit valido (cedente ≠ cessionario).
    private const string PivaStudio  = "01961500236";
    private const string PivaCliente = "12345678903";

    private static AvvisoPdfData CostruisciDati(TipoAnagrafica tipoCliente = TipoAnagrafica.Societa)
    {
        var studio = new Azienda
        {
            NomeBreve          = "Studio",
            RagioneSociale     = "Studio Associato Architetti Test",
            PIVA               = PivaStudio,
            CodiceFiscale      = PivaStudio,
            IndirizzoVia       = "Via Roma",
            IndirizzoCivico    = "1",
            IndirizzoCAP       = "37100",
            IndirizzoComune    = "Verona",
            IndirizzoProvincia = "VR",
            IndirizzoPaese     = "IT",
            RegimeFiscale      = "RF01",
            Email              = "studio@example.it",
            // Profilo fiscale "studio professionale": cassa + ritenuta con i codici FE.
            ApplicaCassaPrevidenziale  = true,
            TipoCassaFe                = "TC04",
            SoggettoARitenuta          = true,
            TipoRitenutaFe             = "RT02",
            CausalePagamentoRitenutaFe = "A",
        };

        var cliente = new Anagrafica
        {
            TipoAnagrafica     = tipoCliente,
            RagioneSociale     = "Cliente Esempio S.r.l.",
            PIVA               = PivaCliente,
            Indirizzo          = "Via Milano 2",
            CAP                = "20100",
            City               = "Milano",
            Provincia          = "MI",
            SiglaPaese         = "IT",
            CodiceDestinatario = "ABCDEFG",
        };

        var testata = new AvvisoFattura
        {
            IdAvviso                 = Guid.NewGuid(),
            IdAttivita               = Guid.NewGuid(),
            IdAnagrafica             = cliente.IdAnagrafica,
            DataAvviso               = new DateOnly(2026, 7, 30),
            Oggetto                  = "Prestazione professionale di progettazione",
            AliquotaIva              = 22m,
            AliquotaCnpaia           = 4m,
            AliquotaRitenuta         = 20m,
            ApplicaRitenuta          = true,
            DescrizioneSpeseInAvviso = "Marca da bollo e diritti",
        };

        var righe = new List<AvvisoFatturaRiga>
        {
            new() { IdRiga = Guid.NewGuid(), IdAvviso = testata.IdAvviso, Ordine = 1,
                    Descrizione = "Progettazione architettonica", Importo = 1000m, IsDescrittiva = false },
            new() { IdRiga = Guid.NewGuid(), IdAvviso = testata.IdAvviso, Ordine = 2,
                    Descrizione = "Nota descrittiva di riga", IsDescrittiva = true },
        };

        var calcolo = new CalcoloFiscaleAvviso().Calcola(new CalcoloFiscaleInput(
            Imponibile: 1000m, AliquotaCassa: 4m, AliquotaIva: 22m, AliquotaRitenuta: 20m,
            ApplicaCassa: true, ApplicaRitenuta: true, SpeseArt15: 16m));

        return new AvvisoPdfData(
            Studio: studio,
            Cliente: cliente,
            Attivita: null,
            Testata: testata,
            Righe: righe,
            DescrizionePagamento: "Bonifico bancario",
            DescrizioneBanca: "Banca Test - IBAN: IT60X0542811101000000123456",
            Calcolo: calcolo,
            Fattura: null,
            BancaIban: "IT60X0542811101000000123456");
    }

    private static Fattura Fattura() => new()
    {
        IdFattura     = Guid.NewGuid(),
        IdAvviso      = Guid.NewGuid(),
        NumeroFattura = 37,
        Anno          = 2026,
        DataFattura   = new DateOnly(2026, 7, 30),
    };

    // Dati di una S.r.l. commerciale (caso ICM Solutions): nessuna cassa, nessuna
    // ritenuta — solo imponibile + IVA + spese escluse.
    private static AvvisoPdfData CostruisciDatiSrl()
    {
        var dati = CostruisciDati();
        var testata = new AvvisoFattura
        {
            IdAvviso         = dati.Testata.IdAvviso,
            IdAttivita       = dati.Testata.IdAttivita,
            IdAnagrafica     = dati.Testata.IdAnagrafica,
            DataAvviso       = dati.Testata.DataAvviso,
            Oggetto          = "Servizi di progettazione",
            AliquotaIva      = 22m,
            AliquotaCnpaia   = 0m,
            AliquotaRitenuta = 0m,
            ApplicaRitenuta  = false,
        };
        var calcolo = new CalcoloFiscaleAvviso().Calcola(new CalcoloFiscaleInput(
            Imponibile: 1000m, AliquotaCassa: 0m, AliquotaIva: 22m, AliquotaRitenuta: 0m,
            ApplicaCassa: false, ApplicaRitenuta: false, SpeseArt15: 0m));
        return dati with { Testata = testata, Calcolo = calcolo };
    }

    [Fact]
    public void Mappa_AvvisoRealistico_ProduceTracciatoValido()
    {
        var (fo, cedentePiva) = FatturaPaXmlService.Mappa(CostruisciDati(), Fattura(), "00001");

        Assert.Equal(PivaStudio, cedentePiva);

        var vr = fo.Validate();
        Assert.True(vr.IsValid,
            "Tracciato non valido: " +
            string.Join(" | ", vr.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")));
    }

    [Fact]
    public void Mappa_ContieneCedenteCassaSpeseEPagamento()
    {
        var (fo, _) = FatturaPaXmlService.Mappa(CostruisciDati(), Fattura(), "00001");
        var xml = Serializza(fo);

        Assert.Contains("FPR12", xml);                 // formato privati/società
        Assert.Contains(PivaStudio, xml);              // cedente/trasmittente
        Assert.Contains(PivaCliente, xml);             // cessionario
        Assert.Contains("TC04", xml);                  // cassa INARCASSA
        Assert.Contains("RT02", xml);                  // ritenuta soggetto diverso da PF
        Assert.Contains("N1", xml);                    // spese escluse art. 15
        Assert.Contains("IT60X0542811101000000123456", xml); // IBAN pagamento
    }

    [Fact]
    public void Mappa_ClienteConCodiceFiscale_ValidoAncheSenzaPartitaIva()
    {
        // Privato con solo codice fiscale (16 char): deve usare CodiceFiscale, non IdFiscaleIVA.
        var dati = CostruisciDati(TipoAnagrafica.Privato);
        var conCf = dati with
        {
            Cliente = new Anagrafica
            {
                TipoAnagrafica     = TipoAnagrafica.Privato,
                RagioneSociale     = "Mario Rossi",
                PIVA               = "RSSMRA80A01H501U", // codice fiscale persona fisica
                Indirizzo          = "Via Verdi 3",
                CAP                = "00100",
                City               = "Roma",
                Provincia          = "RM",
                SiglaPaese         = "IT",
                CodiceDestinatario = "0000000",
            },
        };

        var (fo, _) = FatturaPaXmlService.Mappa(conCf, Fattura(), "00002");
        var vr = fo.Validate();
        Assert.True(vr.IsValid,
            "Tracciato non valido: " +
            string.Join(" | ", vr.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")));
        Assert.Contains("RSSMRA80A01H501U", Serializza(fo));
    }

    [Fact]
    public void Mappa_Srl_SenzaCassaNeRitenuta_ValidaSenzaCodiciConfigurati()
    {
        // Caso ICM Solutions (S.r.l. commerciale): niente cassa né ritenuta. Il
        // tracciato deve essere valido e non contenere i blocchi/codici cassa-ritenuta.
        var (fo, _) = FatturaPaXmlService.Mappa(CostruisciDatiSrl(), Fattura(), "00003");

        var vr = fo.Validate();
        Assert.True(vr.IsValid,
            "Tracciato non valido: " +
            string.Join(" | ", vr.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")));

        var xml = Serializza(fo);
        Assert.DoesNotContain("DatiCassaPrevidenziale", xml);
        Assert.DoesNotContain("DatiRitenuta", xml);
        Assert.DoesNotContain("TC04", xml);
    }

    [Theory]
    [InlineData(0, "00000")]
    [InlineData(1, "00001")]
    [InlineData(35, "0000Z")]  // 35 = 'Z' in base36
    [InlineData(36, "00010")]  // 36 = "10" in base36
    public void CodificaProgressivo_Base36Padded5(long seq, string atteso)
        => Assert.Equal(atteso, FatturaPaXmlService.CodificaProgressivo(seq));

    // ─────────────────────────────────────────────────────────────────────────
    // Ramo Pubblica Amministrazione (FPA12 + split payment)
    // ─────────────────────────────────────────────────────────────────────────

    // Cliente ente pubblico su profilo S.r.l. commerciale (no cassa/ritenuta): il
    // caso tipico di ICM Solutions che fattura a un Comune. Codice Univoco Ufficio
    // IPA a 6 caratteri, P.IVA a 11 cifre.
    private static AvvisoPdfData CostruisciDatiPa(string codiceUfficio = "UFAB12")
    {
        var dati = CostruisciDatiSrl();
        var cliente = new Anagrafica
        {
            TipoAnagrafica     = TipoAnagrafica.EntePubblico,
            RagioneSociale     = "Comune di Verona",
            PIVA               = PivaCliente,   // 11 cifre → IdFiscaleIVA
            Indirizzo          = "Piazza Bra 1",
            CAP                = "37121",
            City               = "Verona",
            Provincia          = "VR",
            SiglaPaese         = "IT",
            CodiceDestinatario = codiceUfficio,
        };
        return dati with { Cliente = cliente };
    }

    private static Fattura FatturaPaConCigCup(string? cig = null, string? cup = null) => new()
    {
        IdFattura     = Guid.NewGuid(),
        IdAvviso      = Guid.NewGuid(),
        NumeroFattura = 5,
        Anno          = 2026,
        DataFattura   = new DateOnly(2026, 7, 30),
        Cig           = cig,
        Cup           = cup,
    };

    [Fact]
    public void Mappa_EntePubblico_ProduceFpa12ConSplitPayment_Valido()
    {
        var (fo, _) = FatturaPaXmlService.Mappa(CostruisciDatiPa(), FatturaPaConCigCup(), "0000A");

        var vr = fo.Validate();
        Assert.True(vr.IsValid,
            "Tracciato non valido: " +
            string.Join(" | ", vr.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")));

        Assert.Equal("FPA12", fo.FatturaElettronicaHeader.DatiTrasmissione.FormatoTrasmissione);
        // Scissione dei pagamenti sul riepilogo della prestazione.
        var riepilogo = fo.FatturaElettronicaBody[0].DatiBeniServizi.DatiRiepilogo[0];
        Assert.Equal("S", riepilogo.EsigibilitaIVA);
    }

    [Fact]
    public void Mappa_EntePubblico_ImportoPagamentoAlNettoDellIva()
    {
        // Srl a un Comune: imponibile 1000, IVA 220, totale 1220. In split payment
        // il fornitore incassa 1000 (la PA versa i 220 di IVA all'Erario).
        var (fo, _) = FatturaPaXmlService.Mappa(CostruisciDatiPa(), FatturaPaConCigCup(), "0000A");

        var dett = fo.FatturaElettronicaBody[0].DatiPagamento[0].DettaglioPagamento[0];
        Assert.Equal(1000m, dett.ImportoPagamento);
    }

    [Fact]
    public void Mappa_EntePubblico_SenzaCodiceUfficioA6Cifre_Lancia()
    {
        // Codice destinatario a 7 caratteri (formato privati): non valido per la PA.
        var dati = CostruisciDatiPa(codiceUfficio: "ABCDEFG");
        Assert.Throws<FatturaPaDatiMancantiException>(
            () => FatturaPaXmlService.Mappa(dati, FatturaPaConCigCup(), "0000A"));
    }

    [Fact]
    public void Mappa_EntePubblico_ConCigCup_FinisconoInDatiOrdineAcquisto()
    {
        var (fo, _) = FatturaPaXmlService.Mappa(
            CostruisciDatiPa(), FatturaPaConCigCup(cig: "1234567890", cup: "B12H34567890123"), "0000A");

        var ordine = fo.FatturaElettronicaBody[0].DatiGenerali.DatiOrdineAcquisto;
        var doc = Assert.Single(ordine);
        Assert.Equal("1234567890", doc.CodiceCIG);
        Assert.Equal("B12H34567890123", doc.CodiceCUP);

        var vr = fo.Validate();
        Assert.True(vr.IsValid,
            "Tracciato non valido: " +
            string.Join(" | ", vr.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")));
    }

    [Fact]
    public void Mappa_Privati_NessunOrdineAcquisto_EsigibilitaImmediata()
    {
        // Regressione: un privato/società resta FPR12, esigibilità "I", niente
        // DatiOrdineAcquisto anche se la fattura portasse CIG/CUP (campi non PA).
        var (fo, _) = FatturaPaXmlService.Mappa(
            CostruisciDati(), FatturaPaConCigCup(cig: "1234567890"), "00001");

        Assert.Equal("FPR12", fo.FatturaElettronicaHeader.DatiTrasmissione.FormatoTrasmissione);
        Assert.Empty(fo.FatturaElettronicaBody[0].DatiGenerali.DatiOrdineAcquisto);
        Assert.Equal("I", fo.FatturaElettronicaBody[0].DatiBeniServizi.DatiRiepilogo[0].EsigibilitaIVA);
    }

    private static string Serializza(FatturaElettronica.Ordinaria.FatturaOrdinaria fo)
    {
        var sb = new StringBuilder();
        using (var w = XmlWriter.Create(sb, new XmlWriterSettings { Indent = true }))
            fo.WriteXml(w);
        return sb.ToString();
    }
}
