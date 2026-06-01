namespace ICMFatturazioni.Web.Repositories.Interfaces;

/// <summary>
/// DTO ultra-leggera per popolare dropdown nelle maschere. Contiene
/// solo le due informazioni necessarie alla selezione: valore di
/// persistenza (Codice/Sigla) e descrizione visibile.
/// </summary>
public sealed record LookupItem(string Codice, string Descrizione);

/// <summary>
/// Letture dei lookup statici dello schema <c>sta</c>. Pensato per
/// alimentare i dropdown delle maschere senza obbligare la UI a passare
/// per un Manager (i lookup sono read-only di sistema e non hanno
/// regole di business associate).
/// </summary>
public interface ILookupRepository
{
    /// <summary>Tutti i paesi (per default Italia in cima).</summary>
    Task<IReadOnlyList<LookupItem>> GetPaesiAsync(CancellationToken cancellationToken = default);

    /// <summary>Tutte le province italiane.</summary>
    Task<IReadOnlyList<LookupItem>> GetProvinceAsync(CancellationToken cancellationToken = default);
}
