namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Dati anagrafici e fiscali dello studio emittente (il "cedente/prestatore"):
/// intestazione dei documenti (avviso di fattura, e in Fase D la fattura
/// elettronica). Origine legacy: <c>dbo.TAB_Azienda</c> del gestionale Access.
/// POCO senza dipendenze da Dapper/EF/ASP.NET.
/// </summary>
/// <remarks>
/// Sistema mono-studio: di norma esiste una sola riga attiva, letta come
/// "l'azienda corrente". I campi per la Fattura Elettronica (REA, CCIAA,
/// capitale sociale, regime fiscale, ...) sono già presenti ma non ancora usati
/// dal PDF di avviso: serviranno alla generazione XML/SdI (Fase D).
/// </remarks>
public sealed class Azienda
{
    /// <summary>Chiave primaria GUID (UUIDv7 app-side, ADR D22).</summary>
    public Guid IdAzienda { get; set; }

    /// <summary>Nome breve/alias interno dello studio (colonna legacy "Azienda").</summary>
    public required string NomeBreve { get; init; }

    /// <summary>Ragione sociale piena, mostrata in intestazione dei documenti.</summary>
    public required string RagioneSociale { get; init; }

    /// <summary>Partita IVA (11 cifre). Nel caso reale coincide col codice fiscale.</summary>
    public string? PIVA { get; init; }

    /// <summary>Codice fiscale (11 o 16 caratteri).</summary>
    public string? CodiceFiscale { get; init; }

    // --- Sede legale (ex campi Indirizzo_* del legacy). ---
    public string? IndirizzoVia { get; init; }
    public string? IndirizzoCivico { get; init; }
    public string? IndirizzoComune { get; init; }

    /// <summary>Sigla provincia (es. "VR").</summary>
    public string? IndirizzoProvincia { get; init; }
    public string? IndirizzoCAP { get; init; }

    /// <summary>Codice paese ISO-2 (es. "IT").</summary>
    public string? IndirizzoPaese { get; init; }

    // --- Recapiti (mail e PEC separate, a differenza del legacy). ---
    public string? Telefono { get; init; }
    public string? Telefax { get; init; }
    public string? Email { get; init; }
    public string? PEC { get; init; }

    // --- Dati per la Fattura Elettronica (Fase D). Non usati dal PDF di avviso. ---
    public string? REA { get; init; }
    public string? REAFe { get; init; }
    public string? CCIAA { get; init; }
    public string? CCIAAFe { get; init; }
    public string? CapitaleSociale { get; init; }
    public string? CapitaleSocialeFe { get; init; }
    public string? RegimeFiscale { get; init; }
    public string? StatoLiquidazione { get; init; }
    public string? SocioUnico { get; init; }
    public string? Identificativo { get; init; }

    /// <summary>Soft-delete (ADR D22). Default <c>true</c>.</summary>
    public bool IsAttivo { get; init; } = true;
}
