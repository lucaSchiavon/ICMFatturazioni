namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Testata di un avviso di fattura (dispensa cap. 5-7): raccoglie le rate
/// (scadenze) da fatturare per una singola attività, applica la cascata fiscale
/// e diventa la base della fattura. Un avviso = una attività.
///
/// I campi fiscali (<see cref="AliquotaIva"/>, <see cref="AliquotaCnpaia"/>,
/// <see cref="AliquotaRitenuta"/>, <see cref="ApplicaRitenuta"/>) e i riferimenti
/// di pagamento sono uno <b>snapshot</b> del momento di emissione: congelano il
/// calcolo così che un avviso storico resti corretto anche se in seguito cambiano
/// l'anagrafica o le aliquote vigenti.
/// </summary>
public sealed class AvvisoFattura
{
    /// <summary>Chiave primaria GUID (UUIDv7 app-side, ADR D22).</summary>
    public Guid IdAvviso { get; set; }

    /// <summary>FK → fatt.Attivita (fisicamente dbo.Progetto): attività fatturata.</summary>
    public Guid IdAttivita { get; init; }

    /// <summary>FK → fatt.Anagrafica (fisicamente dbo.Committente): cliente destinatario (snapshot).</summary>
    public Guid IdAnagrafica { get; init; }

    /// <summary>Data dell'avviso. Obbligatoria.</summary>
    public DateOnly DataAvviso { get; init; }

    /// <summary>Oggetto/intestazione dell'avviso. Facoltativo.</summary>
    public string? Oggetto { get; init; }

    /// <summary>Nota sintetica (riga breve). Facoltativa.</summary>
    public string? NotaSintetica { get; init; }

    /// <summary>Nota estesa di testata. Facoltativa.</summary>
    public string? NotaTestata { get; init; }

    /// <summary>
    /// FK → fatt.CodiciPagamento: condizione di pagamento, ereditata dall'anagrafica
    /// ma modificabile sul singolo avviso (snapshot). Facoltativa.
    /// </summary>
    public Guid? IdCodicePagamento { get; init; }

    /// <summary>
    /// FK → fatt.BancheAppoggio: banca d'appoggio, ereditata dall'anagrafica ma
    /// modificabile sul singolo avviso (snapshot). Facoltativa.
    /// </summary>
    public Guid? IdBancaAppoggio { get; init; }

    /// <summary>Snapshot aliquota IVA all'emissione (es. 22.00 = 22%).</summary>
    public decimal AliquotaIva { get; init; }

    /// <summary>Snapshot aliquota cassa C.N.P.A.I.A. all'emissione (es. 4.00 = 4%).</summary>
    public decimal AliquotaCnpaia { get; init; }

    /// <summary>Snapshot aliquota ritenuta d'acconto all'emissione (es. 20.00 = 20%).</summary>
    public decimal AliquotaRitenuta { get; init; }

    /// <summary>
    /// Snapshot del flag "applica ritenuta" (da <c>Anagrafica.SostitutoImposta</c>
    /// al momento dell'emissione). Se <c>false</c> la ritenuta non viene sottratta.
    /// </summary>
    public bool ApplicaRitenuta { get; init; }

    /// <summary>
    /// Testo art. 15 D.P.R. 633/72 obbligatorio quando all'avviso sono allegate
    /// spese anticipate (riaddebito fuori campo IVA). Facoltativo.
    /// </summary>
    public string? DescrizioneSpeseInAvviso { get; init; }

    /// <summary>Soft-delete (ADR D22). Default <c>true</c>.</summary>
    public bool IsAttivo { get; init; } = true;

    // Proprietà di navigazione (non mappate 1:1 su colonne — popolate da subquery nel Repository).

    /// <summary>
    /// Somma degli importi delle righe dell'avviso (imponibile lordo, pre-cascata).
    /// Subquery di convenienza per l'elenco avvisi; la cascata fiscale completa
    /// è responsabilità del calcolo (CalcoloFiscaleAvviso).
    /// </summary>
    public decimal TotaleRighe { get; set; }

    /// <summary>
    /// Somma delle spese anticipate (art. 15) collegate all'avviso. Subquery di
    /// convenienza per l'elenco: consente di riconoscere e quantificare gli avvisi
    /// di "sole spese" (dove <see cref="TotaleRighe"/> è 0).
    /// </summary>
    public decimal TotaleSpese { get; set; }

    /// <summary>
    /// True quando l'avviso non ha imponibile da prestazione ma ha spese anticipate:
    /// è un avviso di "sole spese art. 15". Derivato dalle due subquery di elenco.
    /// </summary>
    public bool IsSoloSpese => TotaleRighe <= 0.005m && TotaleSpese > 0.005m;
}
