namespace ICMFatturazioni.Web.Authentication;

/// <summary>
/// Risolve l'utente attualmente autenticato (id + nome) a partire dallo stato
/// di autenticazione del circuito Blazor. Astrazione iniettabile così i Manager
/// che fanno audit non dipendono direttamente dall'<c>AuthenticationStateProvider</c>
/// e restano testabili.
/// </summary>
public interface ICurrentUserAccessor
{
    /// <summary>
    /// Restituisce <c>(Id, Nome)</c> dell'utente corrente, o <c>(null, null)</c>
    /// se non autenticato o se il contesto non è disponibile (es. seed
    /// all'avvio, endpoint anonimi).
    /// </summary>
    Task<(Guid? Id, string? Nome)> GetAsync(CancellationToken cancellationToken = default);
}
