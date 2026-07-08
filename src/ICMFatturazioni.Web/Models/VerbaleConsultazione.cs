namespace ICMFatturazioni.Web.Models;

/// <summary>
/// Read-model di una riga della maschera "Consultazione verbali": un verbale
/// firmato dal CSE (non bozza) leggibile da ICMFatturazioni attraverso la vista
/// <c>fatt.VerbaliConsultazione</c> del DB unificato.
/// </summary>
/// <remarks>
/// I verbali sono dominio di ICMVerbali: qui sono in sola lettura. Gli Id dei
/// tre livelli di filtro (anagrafica, attività, cantiere) sono inclusi perché la
/// UI costruisce l'universo dei filtri e restringe la griglia interamente in
/// memoria (i volumi sono piccoli). <see cref="ReportPath"/> è il percorso
/// relativo del PDF archiviato da ICMVerbali: la sua <b>esistenza fisica</b> è
/// il criterio di visibilità (verificata dal Manager, non nella vista).
/// </remarks>
public sealed record VerbaleConsultazione
{
    /// <summary>PK del verbale (dbo.Verbale.Id).</summary>
    public required Guid IdVerbale { get; init; }

    /// <summary>Numero progressivo per cantiere; null solo per firmati legacy senza numero.</summary>
    public int? Numero { get; init; }

    /// <summary>Anno di etichetta del verbale.</summary>
    public int? Anno { get; init; }

    /// <summary>Data del verbale.</summary>
    public required DateOnly Data { get; init; }

    // ── Livello 1: anagrafica (committente) ──────────────────────────────
    public required Guid IdAnagrafica { get; init; }
    public required string RagioneSocialeCliente { get; init; }

    // ── Livello 2: attività (progetto) ───────────────────────────────────
    public required Guid IdAttivita { get; init; }
    public required string NumeroAttivita { get; init; }
    public required string DescrizioneAttivita { get; init; }

    // ── Livello 3: cantiere ──────────────────────────────────────────────
    public required Guid IdCantiere { get; init; }
    public required string UbicazioneCantiere { get; init; }

    /// <summary>Coordinatore per la sicurezza in esecuzione (CSE) che ha firmato.</summary>
    public string? CseNominativo { get; init; }

    /// <summary>Impresa appaltatrice del cantiere per quel verbale.</summary>
    public string? ImpresaAppaltatrice { get; init; }

    /// <summary>Etichetta dell'esito complessivo (es. "Conforme"); null se non impostato.</summary>
    public string? EsitoEtichetta { get; init; }

    /// <summary>Gravità dell'esito (semaforo conformità); null se l'esito non è impostato.</summary>
    public SeveritaEsito? EsitoSeverita { get; init; }

    /// <summary>Numero di prescrizioni CSE associate al verbale.</summary>
    public int NumeroPrescrizioni { get; init; }

    /// <summary>Percorso relativo del PDF archiviato (sotto la cartella uploads di ICMVerbali).</summary>
    public string? ReportPath { get; init; }

    /// <summary>Nome leggibile del verbale ("Verbale N/AAAA"); fallback se manca il numero.</summary>
    public string NomeVerbale => Numero is int n ? $"Verbale {n}/{Anno}" : "Verbale (s.n.)";
}
