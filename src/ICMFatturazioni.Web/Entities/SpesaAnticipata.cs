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

    /// <summary>Soft-delete (ADR D22). Default <c>true</c>.</summary>
    public bool IsAttivo { get; init; } = true;
}
