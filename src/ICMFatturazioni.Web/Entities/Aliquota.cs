namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Aliquota vigente gestita dallo studio (riga di <c>fatt.Aliquote</c>): es. la
/// maggiorazione cassa C.N.P.A.I.A. e la ritenuta d'acconto usate nel calcolo
/// dell'avviso (dispensa cap. 7). Ispirata a <c>STA-Aliquote</c> del gestionale
/// legacy, uniformata alle regole ICMFatturazioni (GUID v7, decimal, soft-delete).
/// </summary>
public sealed class Aliquota
{
    /// <summary>Chiave primaria GUID (UUIDv7 app-side, ADR D22).</summary>
    public Guid IdAliquota { get; set; }

    /// <summary>
    /// Codice stabile per le aliquote "di sistema" usate dal motore di calcolo
    /// (<c>CNPAIA</c>, <c>RITENUTA</c>); <c>null</c> per le aliquote libere
    /// aggiunte dall'utente. Identifica l'aliquota a prescindere dalla descrizione.
    /// </summary>
    public string? Codice { get; init; }

    /// <summary>Descrizione leggibile dell'aliquota. Obbligatoria.</summary>
    public required string Descrizione { get; init; }

    /// <summary>Valore percentuale (es. 4 = 4%, 20 = 20%). Deve essere ≥ 0.</summary>
    public decimal Valore { get; init; }

    /// <summary>Soft-delete (ADR D22). Default <c>true</c>.</summary>
    public bool IsAttivo { get; init; } = true;

    /// <summary>
    /// True se è un'aliquota di sistema (ha un <see cref="Codice"/>): non
    /// eliminabile, perché usata dal calcolo dell'avviso. Valore e descrizione
    /// restano modificabili. Derivata, non mappata sul DB.
    /// </summary>
    public bool IsSistema => !string.IsNullOrEmpty(Codice);
}
