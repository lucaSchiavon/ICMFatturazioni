namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Severità di un evento di errore (mappata sulla colonna
/// <c>dbo.LogErrors.Severity TINYINT</c>).
/// </summary>
public enum Severity : byte
{
    /// <summary>Informazione diagnostica, non un errore.</summary>
    Info = 0,
    /// <summary>Anomalia non bloccante.</summary>
    Warning = 1,
    /// <summary>Errore gestibile (default).</summary>
    Error = 2,
    /// <summary>Errore critico che mette a rischio l'integrità dell'app.</summary>
    Critical = 3,
}

/// <summary>
/// Riga della tabella <c>dbo.LogErrors</c>. POCO senza dipendenze.
/// </summary>
public sealed class LogError
{
    public long Id { get; init; }
    public DateTime TimestampUtc { get; init; }
    public required string ExceptionType { get; init; }
    public required string Message { get; init; }
    public string? StackTrace { get; init; }
    public string? InnerExceptionType { get; init; }
    public string? InnerExceptionMessage { get; init; }
    public string? InnerExceptionStackTrace { get; init; }
    public string? Source { get; init; }
    public string? DescrizioneEstesa { get; init; }
    public string? Contesto { get; init; }
    public int? UserId { get; init; }
    public string? UserName { get; init; }
    public string? RequestPath { get; init; }
    public string? MachineName { get; init; }
    public string? EnvironmentName { get; init; }
    public string? CorrelationId { get; init; }
    public Severity Severity { get; init; } = Severity.Error;
    public bool Handled { get; init; }
}
