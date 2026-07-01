namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Spesa sostenuta dallo studio per conto del cliente (bolli, accessi agli atti)
/// da riaddebitare in fattura (dispensa cap. 2 — "Spese Anticipate Studio").
/// È legata a una singola attività (testata cliente+progetto) e in fase di avviso
/// viene trattata come spesa esclusa ex art. 15 D.P.R. 633/72 (fuori base IVA).
/// </summary>
public sealed class SpesaAnticipata
{
    /// <summary>Chiave primaria GUID (UUIDv7 app-side, ADR D22).</summary>
    public Guid IdSpesaAnticipata { get; set; }

    /// <summary>FK → fatt.Attivita: attività a cui la spesa si riferisce.</summary>
    public Guid IdAttivita { get; init; }

    /// <summary>Data in cui la spesa è stata sostenuta. Obbligatoria.</summary>
    public DateOnly Data { get; init; }

    /// <summary>Descrizione libera della spesa (es. "BOLLO", "ACCESSO AGLI ATTI"). Obbligatoria.</summary>
    public required string Descrizione { get; init; }

    /// <summary>Importo della spesa (€). Deve essere &gt; 0.</summary>
    public decimal Importo { get; init; }

    /// <summary>
    /// FK → fatt.AvvisiFattura: avviso che ha riaddebitato (associato) questa spesa
    /// ("In Avviso Del"). <c>null</c> = spesa ancora disponibile. Quando valorizzata
    /// la spesa è <b>congelata</b> (niente modifica/eliminazione) e non è più
    /// selezionabile da altri avvisi, finché non si annulla l'avviso che la consuma.
    /// </summary>
    public Guid? IdAvviso { get; init; }

    /// <summary>True se la spesa è già associata a un avviso (derivato da <see cref="IdAvviso"/>).</summary>
    public bool IsInAvviso => IdAvviso.HasValue;

    /// <summary>Soft-delete (ADR D22). Default <c>true</c>.</summary>
    public bool IsAttivo { get; init; } = true;

    // --- Nav read-only (non mappate): valorizzate solo dalla lettura per-attività,
    //     per mostrare in UI in quale avviso la spesa è stata associata. ---

    /// <summary>Data dell'avviso che ha associato la spesa (null se non associata).</summary>
    public DateOnly? AvvisoDataAssociazione { get; set; }

    /// <summary>Oggetto dell'avviso che ha associato la spesa (null se non associata o senza oggetto).</summary>
    public string? AvvisoOggettoAssociazione { get; set; }
}
