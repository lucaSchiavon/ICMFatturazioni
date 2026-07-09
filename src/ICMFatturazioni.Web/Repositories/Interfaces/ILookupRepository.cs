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

    /// <summary>
    /// Le nature IVA Agenzia Entrate (codice <c>N1..N7</c> + descrizione).
    /// Alimenta il dropdown "Natura" della maschera Codici IVA, abilitato solo
    /// per le aliquote a 0.
    /// </summary>
    Task<IReadOnlyList<LookupItem>> GetNatureIVAAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Le condizioni di pagamento Agenzia Entrate (codice <c>TP01..</c> +
    /// descrizione). Dropdown della maschera Codici di pagamento.
    /// </summary>
    Task<IReadOnlyList<LookupItem>> GetCondizioniPagamentoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Le modalità di pagamento Agenzia Entrate (codice <c>MP01..</c> +
    /// descrizione). Dropdown della maschera Codici di pagamento.
    /// </summary>
    Task<IReadOnlyList<LookupItem>> GetModalitaPagamentoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// I tipi cassa previdenziale Agenzia Entrate (codice <c>TC01..TC22</c> +
    /// descrizione). Dropdown del profilo fiscale nella maschera Dati azienda.
    /// </summary>
    Task<IReadOnlyList<LookupItem>> GetTipiCassaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// I tipi ritenuta Agenzia Entrate (codice <c>RT01..RT06</c> + descrizione).
    /// Dropdown del profilo fiscale nella maschera Dati azienda.
    /// </summary>
    Task<IReadOnlyList<LookupItem>> GetTipiRitenutaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Le causali di pagamento delle ritenute (modello CU/770: codice
    /// <c>A..ZO</c> + descrizione). Dropdown del profilo fiscale in Dati azienda.
    /// </summary>
    Task<IReadOnlyList<LookupItem>> GetCausaliPagamentoAsync(CancellationToken cancellationToken = default);
}
