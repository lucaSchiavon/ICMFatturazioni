namespace ICMFatturazioni.Web.Models;

/// <summary>
/// Stato delle consulenze a carico Studio (decisione D-C4: aperta = residuo &gt; 0).
/// Usato dalla maschera Schede e dal report "Riepilogo attività consulente".
/// </summary>
public enum FiltroStatoConsulenze
{
    Aperte,
    Chiuse,
    Tutte,
}

/// <summary>
/// Filtro del report "Riepilogo attività consulente" (dispensa cap. 7).
/// </summary>
/// <param name="IdConsulente">Consulente puntuale; null = variante GENERALE (tutti i consulenti).</param>
/// <param name="IdAnagrafica">Raffinamento cliente (null = tutti).</param>
/// <param name="IdAttivita">Raffinamento attività (null = tutte).</param>
/// <param name="Stato">Stato delle consulenze (default legacy: solo aperte).</param>
public sealed record FiltroRiepilogoConsulente(
    Guid? IdConsulente,
    Guid? IdAnagrafica = null,
    Guid? IdAttivita = null,
    FiltroStatoConsulenze Stato = FiltroStatoConsulenze.Aperte);
