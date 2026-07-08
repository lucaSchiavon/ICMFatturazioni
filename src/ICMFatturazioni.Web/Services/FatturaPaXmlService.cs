using System.Globalization;
using System.Text;
using System.Xml;

using FatturaElettronica.Defaults;
using FatturaElettronica.Extensions;
using FatturaElettronica.Ordinaria;

using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.FatturaPa;
using ICMFatturazioni.Web.Managers.Interfaces;

using Microsoft.Extensions.Options;

// Alias sui tipi del tracciato che vengono ISTANZIATI (ogni tipo della libreria
// vive in un namespace omonimo → gli alias evitano ambiguità tipo/namespace).
using FeBody             = FatturaElettronica.Ordinaria.FatturaElettronicaBody.FatturaElettronicaBody;
using DettaglioLinea     = FatturaElettronica.Ordinaria.FatturaElettronicaBody.DatiBeniServizi.DettaglioLinee;
using DatiRiepilogo      = FatturaElettronica.Ordinaria.FatturaElettronicaBody.DatiBeniServizi.DatiRiepilogo;
using DatiCassa          = FatturaElettronica.Ordinaria.FatturaElettronicaBody.DatiGenerali.DatiCassaPrevidenziale;
using DatiRitenuta       = FatturaElettronica.Ordinaria.FatturaElettronicaBody.DatiGenerali.DatiRitenuta;
using DatiPagamentoFe    = FatturaElettronica.Ordinaria.FatturaElettronicaBody.DatiPagamento.DatiPagamento;
using DettaglioPagamento = FatturaElettronica.Ordinaria.FatturaElettronicaBody.DatiPagamento.DettaglioPagamento;
using DatiOrdineAcquistoFe = FatturaElettronica.Ordinaria.FatturaElettronicaBody.DatiGenerali.DatiOrdineAcquisto;

namespace ICMFatturazioni.Web.Services;

/// <summary>
/// Implementazione di <see cref="IFatturaPaXmlService"/>. Riusa il
/// <see cref="AvvisoPdfDataBuilder"/> condiviso per assemblare i dati dalla fattura
/// (cedente = azienda, cessionario = cliente, righe e cascata fiscale dagli snapshot
/// dell'avviso), li mappa sul tracciato <c>FatturaOrdinaria</c> (libreria
/// FatturaElettronica.NET), valida offline e serializza in UTF-8.
///
/// Perimetro Fase D1: solo generazione + salvataggio su file system. Formato
/// <b>FPR12</b> (privati/società): gli enti pubblici sono bloccati a monte.
/// </summary>
public sealed class FatturaPaXmlService : IFatturaPaXmlService
{
    private readonly AvvisoPdfDataBuilder _builder;
    private readonly IFattureManager      _fatture;
    private readonly FatturaPaOptions     _options;
    private readonly ILogManager          _log;

    internal FatturaPaXmlService(
        AvvisoPdfDataBuilder builder,
        IFattureManager fatture,
        IOptions<FatturaPaOptions> options,
        ILogManager log)
    {
        _builder = builder;
        _fatture = fatture;
        _options = options.Value;
        _log     = log;
    }

    // Codici del tracciato FatturaPA fissi (indipendenti dalla categoria del cedente).
    // I codici che DIPENDONO dal cedente (cassa/ritenuta) NON sono qui: arrivano da
    // FatturaPaOptions, così l'app non è implicitamente "da studio professionale".
    private const string TipoDocumentoFattura = "TD01"; // fattura
    private const string Divisa               = "EUR";
    private const string NaturaEsclusaArt15   = "N1";   // operazioni escluse ex art. 15 D.P.R. 633/72
    private const string EsigibilitaImmediata = "I";
    private const string EsigibilitaScissione = "S";    // scissione dei pagamenti (split payment, art. 17-ter)
    private const string CondizioniCompleto   = "TP02"; // pagamento completo
    private const string ModalitaBonifico     = "MP05"; // bonifico
    private const string CodiceDestinatarioNullo = "0000000";
    private const int    LunghezzaCodiceUfficioPA = 6;  // Codice Univoco Ufficio IPA (FPA12)

    /// <inheritdoc/>
    public async Task<GenerazioneXmlRisultato> GeneraAsync(Guid idFattura, CancellationToken ct = default)
    {
        var (data, fattura) = await CaricaAsync(idFattura, ct);

        // Prima creazione → nuovo progressivo dalla sequence; rigenerazione → riuso.
        var progressivo = fattura.ProgressivoInvio
            ?? CodificaProgressivo(await _fatture.ProssimoProgressivoInvioSeqAsync(ct));

        var (fo, cedentePiva) = Mappa(data, fattura, progressivo);

        // Validazione offline (schema + regole FatturaPA della libreria).
        var vr = fo.Validate();
        if (!vr.IsValid)
            throw new FatturaPaXmlNonValidoException(
                vr.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}").ToList());

        var nomeFile = ComponiNomeFile(cedentePiva, progressivo);
        var percorso = ScriviSuFile(fo, nomeFile);

        // Marca lo stato (CreatoXML=1, progressivo, nome file, data UTC) + audit.
        await _fatture.SegnaXmlCreatoAsync(idFattura, progressivo, nomeFile, ct);

        return new GenerazioneXmlRisultato(nomeFile, percorso, progressivo);
    }

    /// <inheritdoc/>
    public async Task<(byte[] Contenuto, string NomeFile)> ScaricaAsync(Guid idFattura, CancellationToken ct = default)
    {
        var fattura = await _fatture.GetByIdAsync(idFattura, ct);
        if (fattura is null || !fattura.IsAttivo)
            throw new FatturaPaXmlNonTrovataException(idFattura);
        if (!fattura.CreatoXML || fattura.ProgressivoInvio is null || fattura.NomeFileXml is null)
            throw new FatturaPaXmlNonGeneratoException(idFattura);

        // File presente su disco → lo servo così com'è.
        var percorso = Path.Combine(_options.CartellaOutput, fattura.NomeFileXml);
        if (File.Exists(percorso))
            return (await File.ReadAllBytesAsync(percorso, ct), fattura.NomeFileXml);

        // File assente (spostato/eliminato) → rigenero al volo dallo stato persistito,
        // riusando il progressivo (nessun consumo di sequence, nessun cambio stato).
        var (data, _) = await CaricaAsync(idFattura, ct);
        var (fo, _)   = Mappa(data, fattura, fattura.ProgressivoInvio);
        return (Serializza(fo), fattura.NomeFileXml);
    }

    /// <inheritdoc/>
    public async Task EliminaAsync(Guid idFattura, CancellationToken ct = default)
    {
        var fattura = await _fatture.GetByIdAsync(idFattura, ct);
        if (fattura is null || !fattura.IsAttivo)
            throw new FatturaPaXmlNonTrovataException(idFattura);

        // Niente XML da eliminare: idempotente (nessun file, nessun reset).
        if (!fattura.CreatoXML)
            return;

        // Nome file catturato PRIMA del reset (dopo, NomeFileXml sarà NULL).
        var nomeFile = fattura.NomeFileXml;

        // Reset dello stato XML sul DB (valida l'esito OK e lancia in quel caso):
        // va prima della cancellazione del file, così un blocco lascia il file intatto.
        await _fatture.ResetXmlAsync(idFattura, ct);

        // File best-effort. File.Delete non solleva se il file non esiste; se fallisce
        // per altri motivi (permessi) il DB è già pulito → resta un file orfano
        // innocuo (verrà ignorato), ma lo si traccia.
        if (!string.IsNullOrEmpty(nomeFile) && !string.IsNullOrWhiteSpace(_options.CartellaOutput))
        {
            try
            {
                File.Delete(Path.Combine(_options.CartellaOutput, nomeFile));
            }
            catch (Exception ex)
            {
                await _log.LogErroreAsync(ex,
                    "Stato XML azzerato sul DB, ma la cancellazione del file XML dal disco è " +
                    "fallita: resta un file orfano (innocuo). Verificare i permessi sulla " +
                    "cartella di output FatturaPA.",
                    "FatturaPaXml.Elimina", entityId: idFattura, entityType: "Fattura", cancellationToken: ct);
            }
        }
    }

    // ── Caricamento + guardie comuni a generazione e download ────────────────
    private async Task<(AvvisoPdfData Data, Fattura Fattura)> CaricaAsync(Guid idFattura, CancellationToken ct)
    {
        var fattura = await _fatture.GetByIdAsync(idFattura, ct);
        if (fattura is null || !fattura.IsAttivo)
            throw new FatturaPaXmlNonTrovataException(idFattura);

        var data = await _builder.CostruisciAsync(fattura.IdAvviso, fattura, ct);

        // Privati/società → FPR12; enti pubblici → FPA12 con split payment. Il ramo
        // è deciso in Mappa dal TipoAnagrafica del cliente (nessun blocco a monte).
        return (data, fattura);
    }

    // ── Mappatura AvvisoPdfData → tracciato FatturaOrdinaria (FPR12 / FPA12) ──
    // Ritorna anche la P.IVA del cedente (serve al nome file). Le grandezze fiscali
    // vengono dalla cascata già calcolata (data.Calcolo), congelata sugli snapshot
    // dell'avviso: qui non si ricalcola nulla, si mappa.
    //
    // Il cliente ente pubblico (TipoAnagrafica='E') attiva il ramo PA: formato FPA12,
    // scissione dei pagamenti (split payment, EsigibilitaIVA='S'), Codice Univoco
    // Ufficio IPA a 6 caratteri obbligatorio, ImportoPagamento al netto dell'IVA
    // (che la PA versa direttamente all'Erario) ed eventuali CIG/CUP dell'appalto.
    internal static (FatturaOrdinaria Fattura, string CedentePiva) Mappa(AvvisoPdfData data, Fattura fattura, string progressivo)
    {
        var studio  = data.Studio;
        var cliente = data.Cliente;
        var t       = data.Testata;
        var c       = data.Calcolo;

        var entePubblico = cliente.TipoAnagrafica == TipoAnagrafica.EntePubblico;

        var cedentePiva = Pulisci(studio.PIVA);
        if (string.IsNullOrEmpty(cedentePiva))
            throw new FatturaPaDatiMancantiException(
                "L'azienda emittente non ha la Partita IVA configurata: impossibile generare l'XML.");

        // FPA12 per la PA (Instance.PubblicaAmministrazione), altrimenti FPR12.
        var fo = FatturaOrdinaria.CreateInstance(
            entePubblico ? Instance.PubblicaAmministrazione : Instance.Privati);
        var header = fo.FatturaElettronicaHeader;

        // ── DatiTrasmissione (il trasmittente coincide col cedente) ──
        var dt = header.DatiTrasmissione;
        dt.IdTrasmittente.IdPaese = "IT";
        dt.IdTrasmittente.IdCodice = cedentePiva;
        dt.ProgressivoInvio = progressivo;
        // FormatoTrasmissione (FPR12/FPA12) già impostato da CreateInstance.
        var codiceDest = Pulisci(cliente.CodiceDestinatario).ToUpperInvariant();
        if (entePubblico)
        {
            // PA: il Codice Univoco Ufficio (IPA) di 6 caratteri è obbligatorio e non
            // ammette il fallback su PEC previsto per i privati.
            if (codiceDest.Length != LunghezzaCodiceUfficioPA)
                throw new FatturaPaDatiMancantiException(
                    $"Il cliente «{cliente.RagioneSociale}» è un ente pubblico: per la fattura " +
                    $"elettronica FPA12 serve il Codice Univoco Ufficio (IPA) di {LunghezzaCodiceUfficioPA} " +
                    $"caratteri. Valore attuale: «{(codiceDest.Length == 0 ? "(vuoto)" : codiceDest)}».");
            dt.CodiceDestinatario = codiceDest;
        }
        else if (!string.IsNullOrEmpty(codiceDest))
        {
            dt.CodiceDestinatario = codiceDest;
        }
        else
        {
            dt.CodiceDestinatario = CodiceDestinatarioNullo;
            if (!string.IsNullOrWhiteSpace(cliente.PECFatturaElettronica))
                dt.PECDestinatario = cliente.PECFatturaElettronica!.Trim();
        }
        dt.ContattiTrasmittente.Email = NullSeVuoto(studio.Email);
        dt.ContattiTrasmittente.Telefono = NullSeVuoto(studio.Telefono);

        // ── CedentePrestatore (lo studio) ──
        var ced = header.CedentePrestatore;
        ced.DatiAnagrafici.IdFiscaleIVA.IdPaese = "IT";
        ced.DatiAnagrafici.IdFiscaleIVA.IdCodice = cedentePiva;
        ced.DatiAnagrafici.CodiceFiscale = NullSeVuoto(studio.CodiceFiscale);
        ced.DatiAnagrafici.Anagrafica.Denominazione = studio.RagioneSociale;
        ced.DatiAnagrafici.RegimeFiscale = NullSeVuoto(studio.RegimeFiscale) ?? "RF01";
        ced.Sede.Indirizzo   = NullSeVuoto(studio.IndirizzoVia);
        ced.Sede.NumeroCivico = NullSeVuoto(studio.IndirizzoCivico);
        ced.Sede.CAP         = NullSeVuoto(studio.IndirizzoCAP);
        ced.Sede.Comune      = NullSeVuoto(studio.IndirizzoComune);
        ced.Sede.Provincia   = NullSeVuoto(studio.IndirizzoProvincia);
        ced.Sede.Nazione     = NullSeVuoto(studio.IndirizzoPaese) ?? "IT";
        ced.Contatti.Telefono = NullSeVuoto(studio.Telefono);
        ced.Contatti.Email    = NullSeVuoto(studio.Email);
        // Iscrizione REA (opzionale): solo se ci sono ufficio + numero.
        var reaUfficio = NullSeVuoto(studio.CCIAA);
        var reaNumero  = NullSeVuoto(studio.REA);
        if (reaUfficio is not null && reaNumero is not null)
        {
            ced.IscrizioneREA.Ufficio = reaUfficio.ToUpperInvariant();
            ced.IscrizioneREA.NumeroREA = reaNumero;
            if (decimal.TryParse(studio.CapitaleSociale, NumberStyles.Any, CultureInfo.InvariantCulture, out var cap))
                ced.IscrizioneREA.CapitaleSociale = cap;
        }

        // ── CessionarioCommittente (il cliente) ──
        var cess = header.CessionarioCommittente;
        var idFiscaleCliente = Pulisci(cliente.PIVA);
        if (idFiscaleCliente.Length == 11 && idFiscaleCliente.All(char.IsDigit))
        {
            cess.DatiAnagrafici.IdFiscaleIVA.IdPaese = "IT";
            cess.DatiAnagrafici.IdFiscaleIVA.IdCodice = idFiscaleCliente;
        }
        else if (!string.IsNullOrEmpty(idFiscaleCliente))
        {
            cess.DatiAnagrafici.CodiceFiscale = idFiscaleCliente.ToUpperInvariant();
        }
        else
        {
            throw new FatturaPaDatiMancantiException(
                $"Il cliente «{cliente.RagioneSociale}» non ha Partita IVA né Codice Fiscale: " +
                "impossibile generare l'XML.");
        }
        cess.DatiAnagrafici.Anagrafica.Denominazione = cliente.RagioneSociale;
        cess.Sede.Indirizzo = NullSeVuoto(cliente.Indirizzo);
        cess.Sede.CAP       = NullSeVuoto(cliente.CAP);
        cess.Sede.Comune    = NullSeVuoto(cliente.City);
        cess.Sede.Provincia = NullSeVuoto(cliente.Provincia);
        cess.Sede.Nazione   = string.IsNullOrWhiteSpace(cliente.SiglaPaese) ? "IT" : cliente.SiglaPaese;

        // ── Body ──
        var body = new FeBody();
        var dgd  = body.DatiGenerali.DatiGeneraliDocumento;
        dgd.TipoDocumento = TipoDocumentoFattura;
        dgd.Divisa = Divisa;
        dgd.Data = fattura.DataFattura.ToDateTime(TimeOnly.MinValue);
        dgd.Numero = fattura.NumeroFattura.ToString(CultureInfo.InvariantCulture);
        // Totale documento lordo (comprensivo di IVA e spese, ANTE ritenuta).
        dgd.ImportoTotaleDocumento = c.Totale + c.SpeseArt15;
        AggiungiCausale(dgd.Causale, t.Oggetto);
        AggiungiCausale(dgd.Causale, t.NotaSintetica);

        // Riferimenti appalto pubblico (CIG/CUP): solo per la PA, se valorizzati.
        // Confluiscono in DatiOrdineAcquisto (2.1.2); l'IdDocumento obbligatorio del
        // blocco è il numero della fattura, non tracciando l'app il numero d'ordine.
        var cig = NullSeVuoto(fattura.Cig);
        var cup = NullSeVuoto(fattura.Cup);
        if (entePubblico && (cig is not null || cup is not null))
        {
            body.DatiGenerali.DatiOrdineAcquisto.Add(new DatiOrdineAcquistoFe
            {
                IdDocumento = fattura.NumeroFattura.ToString(CultureInfo.InvariantCulture),
                CodiceCIG = cig,
                CodiceCUP = cup,
            });
        }

        // Cassa previdenziale (contributo integrativo soggetto a IVA). Il TIPO cassa
        // dipende dalla categoria del cedente → viene dalla configurazione, non è
        // hardcodato. Se il calcolo produce una cassa ma il codice non è configurato,
        // la generazione fallisce con un messaggio chiaro (un tracciato senza TipoCassa
        // sarebbe comunque scartato).
        if (c.Cassa > 0m)
        {
            var tipoCassa = NullSeVuoto(studio.TipoCassaFe)
                ?? throw new FatturaPaDatiMancantiException(
                    "L'avviso applica una cassa previdenziale ma il codice TipoCassa non è " +
                    "configurato sull'azienda (profilo cedente, es. TC04 per INARCASSA).");
            dgd.DatiCassaPrevidenziale.Add(new DatiCassa
            {
                TipoCassa = tipoCassa,
                AlCassa = t.AliquotaCnpaia,
                ImportoContributoCassa = c.Cassa,
                ImponibileCassa = c.Imponibile,
                AliquotaIVA = t.AliquotaIva,
            });
        }

        // Ritenuta d'acconto (solo clienti sostituti d'imposta). Anche il TIPO ritenuta
        // dipende dal cedente (RT01 persona fisica / RT02 soggetti diversi) → configurato.
        if (t.ApplicaRitenuta && c.Ritenuta > 0m)
        {
            var tipoRitenuta = NullSeVuoto(studio.TipoRitenutaFe)
                ?? throw new FatturaPaDatiMancantiException(
                    "L'avviso applica la ritenuta d'acconto ma il codice TipoRitenuta non è " +
                    "configurato sull'azienda (profilo cedente, es. RT02 per studio associato).");
            dgd.DatiRitenuta.Add(new DatiRitenuta
            {
                TipoRitenuta = tipoRitenuta,
                ImportoRitenuta = c.Ritenuta,
                AliquotaRitenuta = t.AliquotaRitenuta,
                CausalePagamento = NullSeVuoto(studio.CausalePagamentoRitenutaFe) ?? "A",
            });
        }

        // Dettaglio linee: prestazioni + righe descrittive (importo 0, decisione C).
        var dbs = body.DatiBeniServizi;
        var numero = 1;
        foreach (var riga in data.Righe)
        {
            var descr = string.IsNullOrWhiteSpace(riga.Descrizione) ? "-" : riga.Descrizione!.Trim();
            var importo = riga.IsDescrittiva ? 0m : (riga.Importo ?? 0m);
            dbs.DettaglioLinee.Add(new DettaglioLinea
            {
                NumeroLinea = numero++,
                Descrizione = descr,
                PrezzoUnitario = importo,
                PrezzoTotale = importo,
                AliquotaIVA = t.AliquotaIva,
            });
        }

        // Spese anticipate escluse art. 15: una linea aggregata N1.
        if (c.SpeseArt15 > 0m)
        {
            dbs.DettaglioLinee.Add(new DettaglioLinea
            {
                NumeroLinea = numero++,
                Descrizione = NullSeVuoto(t.DescrizioneSpeseInAvviso)
                              ?? "Spese anticipate escluse ex art. 15 D.P.R. 633/1972",
                PrezzoUnitario = c.SpeseArt15,
                PrezzoTotale = c.SpeseArt15,
                AliquotaIVA = 0m,
                Natura = NaturaEsclusaArt15,
            });
        }

        // Riepiloghi IVA. Gruppo prestazione (imponibile + cassa) a aliquota piena;
        // gruppo N1 per le spese escluse. Sole spese → solo il gruppo N1.
        if (c.ImponibilePiuCassa > 0m)
        {
            dbs.DatiRiepilogo.Add(new DatiRiepilogo
            {
                AliquotaIVA = t.AliquotaIva,
                ImponibileImporto = c.ImponibilePiuCassa,
                Imposta = c.Iva,
                // PA → scissione dei pagamenti (l'IVA la versa la PA all'Erario).
                EsigibilitaIVA = entePubblico ? EsigibilitaScissione : EsigibilitaImmediata,
            });
        }
        if (c.SpeseArt15 > 0m)
        {
            dbs.DatiRiepilogo.Add(new DatiRiepilogo
            {
                AliquotaIVA = 0m,
                Natura = NaturaEsclusaArt15,
                ImponibileImporto = c.SpeseArt15,
                Imposta = 0m,
                RiferimentoNormativo = "Escluse ex art. 15 D.P.R. 633/1972",
            });
        }

        // Pagamento: importo netto effettivamente dovuto dal cliente (post ritenuta).
        // In split payment (PA) si sottrae anche l'IVA: la PA la versa all'Erario,
        // al fornitore arriva l'imponibile (+ eventuali spese art.15, − ritenuta).
        var importoPagamento = entePubblico
            ? c.TotaleNostroAvere - c.Iva
            : c.TotaleNostroAvere;
        var pagamento = new DatiPagamentoFe { CondizioniPagamento = CondizioniCompleto };
        var dettPag = new DettaglioPagamento
        {
            ModalitaPagamento = ModalitaBonifico,
            ImportoPagamento = importoPagamento,
            Beneficiario = studio.RagioneSociale,
        };
        if (!string.IsNullOrWhiteSpace(data.BancaIban))
            dettPag.IBAN = data.BancaIban!.Replace(" ", string.Empty).ToUpperInvariant();
        pagamento.DettaglioPagamento.Add(dettPag);
        body.DatiPagamento.Add(pagamento);

        fo.FatturaElettronicaBody.Add(body);
        return (fo, cedentePiva);
    }

    private static void AggiungiCausale(IList<string> causali, string? testo)
    {
        var t = NullSeVuoto(testo);
        if (t is null) return;
        // Il tracciato limita ogni Causale a 200 caratteri.
        causali.Add(t.Length > 200 ? t[..200] : t);
    }

    // ── Serializzazione / file ───────────────────────────────────────────────
    private string ScriviSuFile(FatturaOrdinaria fo, string nomeFile)
    {
        var cartella = _options.CartellaOutput;
        if (string.IsNullOrWhiteSpace(cartella))
            throw new FatturaPaDatiMancantiException(
                "Cartella di output XML non configurata (FatturaPA:CartellaOutput).");

        Directory.CreateDirectory(cartella);
        var percorso = Path.Combine(cartella, nomeFile);
        File.WriteAllBytes(percorso, Serializza(fo));
        return percorso;
    }

    // FatturaPA richiede UTF-8; nessun BOM (lo SdI lo tollera ma è più pulito senza).
    private static byte[] Serializza(FatturaOrdinaria fo)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        };
        using var ms = new MemoryStream();
        using (var w = XmlWriter.Create(ms, settings))
            fo.WriteXml(w);
        return ms.ToArray();
    }

    // Nome file: IdPaese + IdCodice trasmittente + "_" + progressivo + ".xml".
    private static string ComponiNomeFile(string cedentePiva, string progressivo)
        => $"IT{cedentePiva}_{progressivo}.xml";

    // Progressivo invio: codifica base36 (0-9A-Z) del valore della sequence, su 5
    // caratteri (0-padded a sinistra). Univoco perché la sequence è monotona.
    internal static string CodificaProgressivo(long seq)
    {
        const string alfabeto = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        if (seq < 0)
            throw new InvalidOperationException("Progressivo invio negativo non valido.");

        var sb = new StringBuilder();
        var v = seq;
        do
        {
            sb.Insert(0, alfabeto[(int)(v % 36)]);
            v /= 36;
        } while (v > 0);

        var s = sb.ToString();
        if (s.Length > 5)
            throw new InvalidOperationException(
                "Progressivo invio esaurito: superati i 5 caratteri ammessi dal tracciato.");
        return s.PadLeft(5, '0');
    }

    // ── Helper stringhe ──────────────────────────────────────────────────────
    private static string Pulisci(string? s) => (s ?? string.Empty).Trim();
    private static string? NullSeVuoto(string? s) => string.IsNullOrWhiteSpace(s) ? null : s!.Trim();
}
