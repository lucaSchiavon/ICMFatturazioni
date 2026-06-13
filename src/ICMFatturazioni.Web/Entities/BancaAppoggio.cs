namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Banca di appoggio: legame tra un istituto/filiale (<see cref="Banca"/> /
/// <see cref="Agenzia"/>) e un intestatario (l'Azienda o un cliente). POCO
/// senza dipendenze da Dapper/EF/ASP.NET.
/// </summary>
/// <remarks>
/// Modello normalizzato (decisione 2026-06-13): i dati di banca/agenzia (Nome,
/// ABI, CAB) NON stanno qui ma nelle rispettive anagrafiche, referenziate per
/// id. Qui resta il solo legame + l'IBAN (presente per le sole banche azienda).
/// Discriminante azienda/cliente:
/// <list type="bullet">
///   <item><c>IdCliente == null</c> → banca dell'<b>Azienda</b> (+ IBAN).</item>
///   <item><c>IdCliente</c> valorizzato → banca <b>di quel cliente</b>.</item>
/// </list>
/// Per la lettura "ricca" (con i nomi/codici risolti) si usa il modello
/// <c>Models.BancaAppoggioRiga</c>.
/// </remarks>
public sealed class BancaAppoggio
{
    /// <summary>Chiave primaria GUID (UUIDv7 app-side, ADR D22).</summary>
    public Guid IdBancaAppoggio { get; set; }

    /// <summary>
    /// Cliente intestatario (FK → <c>fatt.Anagrafica</c>). <c>null</c> = banca
    /// dell'Azienda; valorizzato = banca di quel cliente.
    /// </summary>
    public Guid? IdCliente { get; init; }

    /// <summary>Istituto (FK → <c>fatt.Banche</c>). Obbligatorio.</summary>
    public required Guid IdBanca { get; init; }

    /// <summary>Filiale (FK → <c>fatt.Agenzie</c>). Facoltativa.</summary>
    public Guid? IdAgenzia { get; init; }

    /// <summary>
    /// IBAN del conto. Presente sulle sole banche azienda; per le banche cliente
    /// il manager lo forza a <c>null</c>.
    /// </summary>
    public string? IBAN { get; init; }

    /// <summary>Soft-delete (ADR D22). Default <c>true</c>.</summary>
    public bool IsAttivo { get; init; } = true;

    /// <summary>Stato derivato: <c>true</c> se è una banca dell'Azienda.</summary>
    public bool IsBancaAzienda => IdCliente is null;
}
