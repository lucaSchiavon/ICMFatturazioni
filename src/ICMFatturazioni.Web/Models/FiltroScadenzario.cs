using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Models;

/// <summary>
/// Filtro su scadute/non scadute della maschera "Stampa scadenze"
/// (spec "Stampa scadenze.pdf"): scaduta = <c>DataScadenza &lt; oggi</c>.
/// </summary>
public enum FiltroScadute
{
    /// <summary>Nessun filtro sulla data rispetto a oggi.</summary>
    Tutte,

    /// <summary>Solo scadenze con data anteriore a oggi.</summary>
    SoloScadute,

    /// <summary>Solo scadenze con data pari o successiva a oggi.</summary>
    SoloNonScadute,
}

/// <summary>
/// Filtro su evase/non evase della maschera "Stampa scadenze": evasa =
/// rata consumata da un avviso di fattura <b>attivo</b> (<c>IdAvvisoRiga</c>
/// valorizzato; l'annullo dell'avviso azzera il marcatore).
/// </summary>
public enum FiltroEvase
{
    /// <summary>Sia evase che non evase.</summary>
    Entrambe,

    /// <summary>Solo rate già consumate da un avviso attivo.</summary>
    SoloEvase,

    /// <summary>Solo rate ancora da evadere.</summary>
    SoloNonEvase,
}

/// <summary>
/// Criteri di selezione dello scadenzario attività clienti (maschera
/// "Stampa scadenze" e report PDF "Scadenziario attività clienti").
/// Tutti i criteri sono opzionali: il default (istanza vuota) è
/// "tutti i clienti, tutte le attività, tutte le date, tutte, entrambe".
/// </summary>
/// <param name="TipoCliente">Tipologia cliente (S/P/E); null = tutti i tipi.</param>
/// <param name="IdAnagrafica">Cliente specifico; null = tutti i clienti.</param>
/// <param name="IdTipoAttivita">Tipo attività (CONSULENZE, PROGETTAZIONI, …); null = tutti.</param>
/// <param name="IdAttivita">Attività specifica (fatt.Attivita); null = tutte le attività.</param>
/// <param name="DallaData">Limite inferiore (incluso) sulla data scadenza; null = nessuno.</param>
/// <param name="AllaData">Limite superiore (incluso) sulla data scadenza; null = nessuno.</param>
/// <param name="Scadute">Filtro scadute/non scadute rispetto a oggi.</param>
/// <param name="Evase">Filtro evase/non evase (associazione a un avviso attivo).</param>
public sealed record FiltroScadenzario(
    TipoAnagrafica? TipoCliente    = null,
    Guid?           IdAnagrafica   = null,
    Guid?           IdTipoAttivita = null,
    Guid?           IdAttivita     = null,
    DateOnly?       DallaData      = null,
    DateOnly?       AllaData       = null,
    FiltroScadute   Scadute        = FiltroScadute.Tutte,
    FiltroEvase     Evase          = FiltroEvase.Entrambe);
