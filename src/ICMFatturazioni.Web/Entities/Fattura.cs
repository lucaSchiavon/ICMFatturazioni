namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Fattura di cortesia generata da un avviso di fattura (dispensa cap. 8), ultimo
/// anello del ciclo. Non duplica il contenuto dell'avviso: ne è il "certificato di
/// fatturazione" con NUMERO progressivo annuale + DATA. Il PDF prodotto è di
/// cortesia («DOCUMENTO NON VALIDO AI FINI FISCALI»); l'emissione fiscale vera
/// (XML/SdI) è la Fase D, per cui restano i flag <see cref="CreatoXML"/>/<see cref="EsitoXML"/>.
///
/// Un avviso ha al più UNA fattura attiva (indice univoco filtrato lato DB).
/// Annullare la fattura (soft-delete) riapre l'avviso alla rifatturazione.
/// </summary>
public sealed class Fattura
{
    /// <summary>Chiave primaria GUID (UUIDv7 app-side, ADR D22).</summary>
    public Guid IdFattura { get; set; }

    /// <summary>FK → fatt.AvvisiFattura: avviso da cui nasce la fattura (1:1).</summary>
    public Guid IdAvviso { get; init; }

    /// <summary>Numero progressivo della fattura nell'anno. Univoco per (Anno, Numero).</summary>
    public int NumeroFattura { get; init; }

    /// <summary>Anno solare della fattura, base della numerazione progressiva.</summary>
    public int Anno { get; init; }

    /// <summary>Data della fattura. Obbligatoria.</summary>
    public DateOnly DataFattura { get; init; }

    /// <summary>Flag Fattura Elettronica (Fase D): XML prodotto. Default <c>false</c>.</summary>
    public bool CreatoXML { get; init; }

    /// <summary>Esito invio SdI (Fase D). Default 0 = non inviata/in attesa; 1 = esito OK.</summary>
    public int EsitoXML { get; init; }

    // ── Metadati del tracciato XML FatturaPA (Fase D1, migration 066) ──────────
    // Valorizzati alla generazione del file; null finché l'XML non è mai stato creato.

    /// <summary>
    /// Progressivo invio (max 5 caratteri A-Z/0-9) che rende univoco il nome del
    /// file agli occhi dello SdI. SLEGATO dal numero fattura. Assegnato alla PRIMA
    /// creazione dalla sequence <c>fatt.SeqProgressivoInvio</c> e RIUSATO su "Ricrea".
    /// </summary>
    public string? ProgressivoInvio { get; init; }

    /// <summary>Nome del file XML prodotto (<c>IdPaese+IdCodiceTrasmittente_progressivo.xml</c>).</summary>
    public string? NomeFileXml { get; init; }

    /// <summary>Istante (UTC) dell'ultima creazione/rigenerazione del tracciato XML.</summary>
    public DateTime? DataCreazioneXmlUtc { get; init; }

    /// <summary>Istante (UTC) in cui è stato confermato l'esito OK dell'invio allo SdI.</summary>
    public DateTime? DataEsitoXmlUtc { get; init; }

    /// <summary>Soft-delete (ADR D22). Default <c>true</c>.</summary>
    public bool IsAttivo { get; init; } = true;
}
