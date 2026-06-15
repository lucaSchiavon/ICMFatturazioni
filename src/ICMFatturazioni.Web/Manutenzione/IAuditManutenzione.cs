namespace ICMFatturazioni.Web.Manutenzione;

/// <summary>
/// Esito di un ciclo di manutenzione dell'audit.
/// </summary>
/// <param name="RigheEliminate">Righe di audit rimosse dalla retention temporale.</param>
/// <param name="DimensioneDatiMb">Dimensione corrente dei file dati del DB (MB).</param>
/// <param name="AllarmeEmesso">
/// <c>true</c> se la dimensione ha superato la soglia e si è emesso il Warning.
/// </param>
public sealed record EsitoManutenzione(int RigheEliminate, int DimensioneDatiMb, bool AllarmeEmesso);

/// <summary>
/// Stato della dimensione del database rispetto alla soglia di allarme. È la
/// proiezione di sola lettura usata dalla UI (banner in <c>/admin/audit</c>):
/// non purga e non logga.
/// </summary>
/// <param name="DimensioneDatiMb">Dimensione corrente dei file dati del DB (MB).</param>
/// <param name="SogliaGb">Soglia di allarme configurata (GB).</param>
/// <param name="Allarme"><c>true</c> se la dimensione ha raggiunto/superato la soglia.</param>
public sealed record StatoDimensione(int DimensioneDatiMb, int SogliaGb, bool Allarme);

/// <summary>
/// Logica (testabile, fuori dal <see cref="BackgroundService"/>) di un ciclo di
/// manutenzione di <c>fatt.Audit</c>: applica la retention temporale e valuta la
/// sentinella sulla dimensione del database. Scoped: usa Manager/Repository
/// scoped come tutto il resto.
/// </summary>
public interface IAuditManutenzione
{
    Task<EsitoManutenzione> EseguiAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Legge la dimensione corrente del DB e la confronta con la soglia, SENZA
    /// effetti collaterali (non purga, non logga). Per il banner di avviso in UI.
    /// </summary>
    Task<StatoDimensione> ValutaDimensioneAsync(CancellationToken cancellationToken = default);
}
