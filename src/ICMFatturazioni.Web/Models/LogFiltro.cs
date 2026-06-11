using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Models;

/// <summary>
/// Criteri di ricerca per la consultazione di <c>fatt.Log</c> (pagina
/// <c>/admin/log</c>). Tutti i filtri sono opzionali e si combinano in AND.
/// </summary>
public sealed record LogFiltro
{
    /// <summary>Limite inferiore (incluso) su <c>TimestampUtc</c>.</summary>
    public DateTime? DaUtc { get; init; }

    /// <summary>Limite superiore (escluso) su <c>TimestampUtc</c>.</summary>
    public DateTime? AUtc { get; init; }

    /// <summary>Filtro esatto sul livello (Warning/Error/Critical).</summary>
    public LogLivello? Livello { get; init; }

    /// <summary>Match parziale (LIKE) sulla sorgente/categoria.</summary>
    public string? SorgenteContiene { get; init; }

    /// <summary>Match parziale (LIKE) su messaggio o spiegazione.</summary>
    public string? Testo { get; init; }

    public int Pagina { get; init; } = 1;
    public int Dimensione { get; init; } = 25;
}

/// <summary>Pagina di risultati della ricerca log (righe + totale complessivo).</summary>
public sealed record LogRisultato(IReadOnlyList<Log> Righe, int TotaleRighe);
