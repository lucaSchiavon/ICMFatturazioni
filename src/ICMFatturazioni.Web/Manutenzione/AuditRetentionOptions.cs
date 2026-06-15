namespace ICMFatturazioni.Web.Manutenzione;

/// <summary>
/// Configurazione della retention di <c>fatt.Audit</c> e della sentinella sulla
/// dimensione del database. Bindata dalla sezione <c>AuditRetention</c> di
/// appsettings.json (migration 024 / nota tecnica audit-dimensionamento).
/// </summary>
public sealed class AuditRetentionOptions
{
    public const string SectionName = "AuditRetention";

    /// <summary>
    /// Abilita il job automatico di purga periodica. Se <c>false</c>, resta solo
    /// il pulsante manuale in <c>/admin/audit</c> (la retention non avviene da
    /// sola). Default <c>true</c>.
    /// </summary>
    public bool JobAbilitato { get; set; } = true;

    /// <summary>
    /// Finestra di conservazione: le righe di audit più vecchie di tanti mesi
    /// vengono eliminate. Default 36 (3 anni): con compressione PAGE + modify-delta
    /// lo spazio non è un vincolo, quindi la finestra è generosa.
    /// </summary>
    public int MesiConservazione { get; set; } = 36;

    /// <summary>
    /// Cadenza del job automatico, in ore. Default 24 (una volta al giorno): la
    /// retention è idempotente, non serve girare più spesso.
    /// </summary>
    public int IntervalloOreJob { get; set; } = 24;

    /// <summary>
    /// Soglia di allarme sulla dimensione TOTALE dei file dati del database, in GB.
    /// Superata, il job scrive un Warning in <c>fatt.Log</c> (visibile in
    /// <c>/admin/log</c>): è il vero collo di bottiglia di SQL Express (tetto 10 GB),
    /// non il conteggio righe dell'audit. Default 8 (80% del tetto). La sentinella
    /// AVVISA soltanto: non cancella nulla.
    /// </summary>
    public int SogliaAllarmeGb { get; set; } = 8;
}
