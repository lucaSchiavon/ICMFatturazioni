namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Codice IVA del catalogo amministrativo (dispensa cap. 6). POCO senza
/// dipendenze da Dapper/EF/ASP.NET.
/// </summary>
/// <remarks>
/// Regola di dominio (dispensa §6.2): la <see cref="Natura"/> è condizionale e
/// si valorizza <b>solo</b> quando <see cref="Aliquota"/> è 0 (operazione non
/// imponibile/esente). Per un'aliquota imponibile (&gt; 0) la Natura resta
/// <c>null</c>. La regola è imposta sia dal manager (messaggio user-friendly)
/// sia dal CHECK <c>CK_CodiciIVA_NaturaAliquota</c> a DB (doppia difesa).
/// </remarks>
public sealed class CodiceIVA
{
    /// <summary>
    /// Chiave primaria GUID (UUIDv7 time-ordered, generato app-side con
    /// <c>Guid.CreateVersion7()</c> dal manager). Settabile perché il manager
    /// assegna l'Id in fase di creazione; il resto dell'entità è immutabile.
    /// </summary>
    public Guid IdCodiceIVA { get; set; }

    /// <summary>
    /// Sigla del codice IVA. Di norma coincide con l'aliquota ("21", "22") ma
    /// può essere alfanumerica libera ("A", "8a"). Obbligatoria e univoca tra
    /// i codici attivi.
    /// </summary>
    public required string Codice { get; init; }

    /// <summary>
    /// Descrizione che appare in fattura (es. "IVA 22%"). Obbligatoria
    /// (dispensa §6.1): non è derivata né opzionale.
    /// </summary>
    public required string Descrizione { get; init; }

    /// <summary>
    /// Aliquota percentuale. <c>0</c> = operazione non imponibile/esente (e in
    /// tal caso <see cref="Natura"/> è obbligatoria).
    /// </summary>
    public required decimal Aliquota { get; init; }

    /// <summary>
    /// Natura IVA (codice Agenzia Entrate N1..N7, FK → <c>fatt.NatureIVA.Natura</c>).
    /// Valorizzata solo se <see cref="Aliquota"/> = 0, altrimenti <c>null</c>.
    /// </summary>
    public string? Natura { get; init; }

    /// <summary>
    /// Obbligo di bollo, a tre stati: <c>null</c> = non impostato, <c>true</c> =
    /// sì, <c>false</c> = no. Pertinente solo alle operazioni non imponibili
    /// (Aliquota = 0); per le aliquote imponibili il manager lo forza a
    /// <c>null</c>. Serve a marcare il bollo nell'XML della fattura elettronica.
    /// </summary>
    public bool? ObbligoBollo { get; init; }

    /// <summary>
    /// Soft-delete (ADR D22): <c>true</c> = attivo, <c>false</c> = disattivato.
    /// I codici IVA non si cancellano fisicamente. Default <c>true</c>.
    /// </summary>
    public bool IsAttivo { get; init; } = true;
}
