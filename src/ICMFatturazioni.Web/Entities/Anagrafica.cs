namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Anagrafica cliente — entità principale del modulo. POCO senza
/// dipendenze da Dapper, EF, ASP.NET.
/// </summary>
/// <remarks>
/// I campi <c>IdPag</c>, <c>IdBancaAppoggio</c>, <c>IdCodiciIVA</c>,
/// <c>IdTipologieClientela</c> non hanno FK in migration 005: le tabelle
/// parent verranno create in Fase 3. Le FK sono additive — verranno
/// aggiunte con migration ALTER TABLE. Fino ad allora i valori sono
/// "puntatori liberi" e la responsabilità di mantenerli coerenti è del
/// codice applicativo.
/// </remarks>
public sealed class Anagrafica
{
    /// <summary>
    /// Chiave primaria GUID (UUIDv7 time-ordered, generato app-side con
    /// <c>Guid.CreateVersion7()</c> dal manager). Settabile perché il manager
    /// assegna l'Id in fase di creazione; il resto dell'entità è immutabile.
    /// Uniformità con ICMVerbali (ADR D22).
    /// </summary>
    public Guid IdAnagrafica { get; set; }

    /// <summary>
    /// Tipologia: Società / Privato / Ente pubblico.
    /// Persistita come <c>CHAR(1)</c> sul DB tramite
    /// <see cref="TipoAnagraficaExtensions.ToDbCode"/>.
    /// </summary>
    public required TipoAnagrafica TipoAnagrafica { get; init; }

    /// <summary>
    /// Ragione sociale (società/enti) o nome+cognome (privati).
    /// Campo obbligatorio, sfondo verde nella UI legacy.
    /// </summary>
    public required string RagioneSociale { get; init; }

    public string? Indirizzo { get; init; }
    public string? CAP { get; init; }
    public string? City { get; init; }

    /// <summary>Sigla provincia (FK → <c>fatt.Province.Prov</c>).</summary>
    public string? Provincia { get; init; }

    /// <summary>
    /// Codice paese ISO-2 (FK → <c>fatt.Paesi.CodicePaese</c>).
    /// Default applicativo: <c>"IT"</c> (decisione D14: l'azienda lavora
    /// prevalentemente con clienti italiani).
    /// </summary>
    public string SiglaPaese { get; init; } = "IT";

    public string? Telefono { get; init; }
    public string? Cellulare { get; init; }
    public string? Fax { get; init; }
    public string? Email { get; init; }

    /// <summary>
    /// Campo unificato Partita IVA / Codice Fiscale. NVARCHAR(20) sul DB:
    /// copre sia PIVA (11 cifre) che CF (16 caratteri).
    /// </summary>
    public string? PIVA { get; init; }

    /// <summary>Riferimento operativo (es. "Ufficio acquisti — sig. Rossi").</summary>
    public string? Contatto { get; init; }

    /// <summary>Codice del pagamento associato (FK futura).</summary>
    public Guid? IdPag { get; init; }
    /// <summary>Banca di appoggio del cliente (FK futura).</summary>
    public Guid? IdBancaAppoggio { get; init; }
    /// <summary>Codice IVA di default (FK futura).</summary>
    public Guid? IdCodiciIVA { get; init; }
    /// <summary>Tipologia clientela Agenzia Entrate (FK futura).</summary>
    public Guid? IdTipologieClientela { get; init; }

    /// <summary>Codice destinatario SDI per fatturazione elettronica (7 char).</summary>
    public string? CodiceDestinatario { get; init; }

    /// <summary>
    /// PEC alternativa al codice destinatario per fatturazione elettronica.
    /// Refuso "PerFatturaEletronica" del DB legacy corretto come da
    /// CLAUDE.md.
    /// </summary>
    public string? PECFatturaElettronica { get; init; }

    /// <summary>
    /// True se il cliente è sostituto d'imposta (tipicamente imprese/enti): solo
    /// in tal caso l'avviso applica la ritenuta d'acconto (dispensa cap. 7).
    /// Default derivato dal tipo (Privato → false, Società/Ente → true) ma
    /// sovrascrivibile per i casi particolari. Default applicativo <c>true</c>.
    /// </summary>
    public bool SostitutoImposta { get; init; } = true;

    /// <summary>
    /// Soft-delete (ADR D22): <c>true</c> = attiva, <c>false</c> = disattivata.
    /// Le anagrafiche non si cancellano fisicamente. Default <c>true</c>.
    /// </summary>
    public bool IsAttivo { get; init; } = true;
}
