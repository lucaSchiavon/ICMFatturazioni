namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Modalità di gestione di un tipo di attività (cap. 9.1, Fig. 10 dispensa).
/// Determina il comportamento del gestionale per le attività di quel tipo.
/// Persistito come NVARCHAR(20) con i valori stringa dell'enum (es. "Consulenza").
/// </summary>
public enum GestisciCome
{
    /// <summary>Attività di consulenza (prestazione intellettuale, nessun appalto).</summary>
    Consulenza,

    /// <summary>Attività di progettazione (legata a un progetto edilizio con date e importo opera).</summary>
    Progetto,
}
