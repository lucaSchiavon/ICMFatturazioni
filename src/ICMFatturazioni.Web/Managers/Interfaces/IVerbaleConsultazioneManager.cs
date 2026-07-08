using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Web.Managers.Interfaces;

/// <summary>
/// Business logic per la consultazione dei verbali firmati da ICMFatturazioni
/// (dominio di ICMVerbali, letto in sola lettura). Filtra i candidati all'export
/// mantenendo solo i verbali il cui PDF esiste <b>fisicamente</b> su disco: i
/// firmati senza file sono dati legacy e non vengono mai mostrati né rigenerati.
/// </summary>
public interface IVerbaleConsultazioneManager
{
    /// <summary>
    /// Tutti i verbali firmati esportabili (PDF presente su disco), ordinati per
    /// data discendente. La UI ne deriva l'universo dei filtri e restringe la
    /// griglia in memoria.
    /// </summary>
    Task<IReadOnlyList<VerbaleConsultazione>> ElencoEsportabiliAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sottoinsieme di <see cref="ElencoEsportabiliAsync"/> ristretto al livello
    /// di filtro selezionato: obbligatoria l'anagrafica, opzionali attività e
    /// cantiere (se null il filtro a quel livello non si applica). Usato
    /// dall'endpoint ZIP per ricostruire lato server esattamente ciò che l'utente
    /// vede in griglia.
    /// </summary>
    Task<IReadOnlyList<VerbaleConsultazione>> ElencoPerFiltroAsync(
        Guid idAnagrafica,
        Guid? idAttivita,
        Guid? idCantiere,
        CancellationToken cancellationToken = default);
}
