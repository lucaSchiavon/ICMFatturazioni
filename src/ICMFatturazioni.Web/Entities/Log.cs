namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Livello di gravità di una riga di log. I valori numerici coincidono
/// <b>esattamente</b> con <see cref="Microsoft.Extensions.Logging.LogLevel"/>
/// (Warning=3, Error=4, Critical=5): questo consente al <c>DbLogger</c> di
/// fare un cast diretto senza tabella di conversione. Persistiamo solo
/// Warning e oltre.
/// </summary>
public enum LogLivello : byte
{
    /// <summary>Anomalia non bloccante.</summary>
    Warning = 3,
    /// <summary>Errore gestibile.</summary>
    Error = 4,
    /// <summary>Errore critico (mette a rischio l'integrità o termina il processo).</summary>
    Critical = 5,
}

/// <summary>
/// Riga della tabella <c>fatt.Log</c>. POCO senza dipendenze. Immutabile per
/// convenzione applicativa: si inserisce e non si aggiorna mai.
/// </summary>
public sealed class Log
{
    public Guid Id { get; set; }
    public DateTime TimestampUtc { get; set; }
    public LogLivello Livello { get; set; }

    /// <summary>Categoria del logger o sorgente esplicita (es. "Auth.ForgotPassword").</summary>
    public string Sorgente { get; set; } = string.Empty;
    public string Messaggio { get; set; } = string.Empty;

    /// <summary>Full name del tipo di eccezione, se l'evento ne porta una.</summary>
    public string? EccezioneTipo { get; set; }
    public string? StackTrace { get; set; }

    /// <summary>
    /// Spiegazione user-friendly: valorizzata solo dal path esplicito
    /// <c>ILogManager.LogErroreAsync</c>, mai dalla rete automatica del provider.
    /// </summary>
    public string? SpiegazioneUtente { get; set; }

    /// <summary>Correlazione con la request/attività (<c>Activity.Current?.Id</c>).</summary>
    public string? RequestId { get; set; }

    public Guid? UtenteId { get; set; }

    /// <summary>Entità di dominio coinvolta (facoltativa).</summary>
    public Guid? EntityId { get; set; }
    public string? EntityType { get; set; }
}
