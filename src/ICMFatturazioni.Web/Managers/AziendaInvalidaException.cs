namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Motivo per cui il salvataggio dei dati del cedente (<c>fatt.Azienda</c>) è
/// stato rifiutato. L'ordine segue la regola CLAUDE.md ("Ordine dei controlli
/// in eccezioni tipizzate è UX"): il primo controllo che fallisce è il messaggio
/// mostrato all'utente.
/// </summary>
public enum AziendaInvalidaMotivo
{
    /// <summary>La ragione sociale è vuota o solo whitespace.</summary>
    RagioneSocialeObbligatoria,

    /// <summary>Il nome breve (alias interno) è vuoto o solo whitespace.</summary>
    NomeBreveObbligatorio,

    /// <summary>La partita IVA, se valorizzata, non è nel formato corretto.</summary>
    PivaNonValida,

    /// <summary>Il codice fiscale, se valorizzato, non è nel formato corretto.</summary>
    CodiceFiscaleNonValido,

    /// <summary>Il CAP, se valorizzato, non è di 5 cifre.</summary>
    CapNonValido,

    /// <summary>L'email, se valorizzata, non è nel formato corretto.</summary>
    EmailNonValida,

    /// <summary>La PEC, se valorizzata, non è nel formato corretto.</summary>
    PecNonValida,
}

/// <summary>
/// Eccezione tipizzata di validazione sul salvataggio del cedente
/// (<c>fatt.Azienda</c>). Flusso previsto: NON va loggata (Regola 6).
/// </summary>
public sealed class AziendaInvalidaException : Exception
{
    public AziendaInvalidaMotivo Motivo { get; }

    public AziendaInvalidaException(AziendaInvalidaMotivo motivo, string message)
        : base(message) => Motivo = motivo;
}
