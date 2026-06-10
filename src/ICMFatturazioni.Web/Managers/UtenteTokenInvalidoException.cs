namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Motivo per cui un magic-link (attivazione/reset) non è utilizzabile.
/// L'ORDINE di valutazione in <c>UtenteTokenManager.ValidaAsync</c> è UX
/// (CLAUDE.md): la revoca esplicita prevale sui motivi temporali, perché
/// "link sostituito, usa l'ultimo" è più azionabile di "link scaduto".
/// </summary>
public enum UtenteTokenInvalidoMotivo : byte
{
    NonTrovato = 0,
    Revocato = 1,
    GiaUsato = 2,
    Scaduto = 3,
}

/// <summary>
/// Sollevata quando un magic-link non è valido. Eccezione di validazione
/// tipizzata: porta un messaggio user-friendly e il <see cref="Motivo"/>, e
/// NON va loggata (vedi CLAUDE.md Regola 6).
/// </summary>
public sealed class UtenteTokenInvalidoException : Exception
{
    public UtenteTokenInvalidoException(UtenteTokenInvalidoMotivo motivo, string message)
        : base(message) => Motivo = motivo;

    public UtenteTokenInvalidoMotivo Motivo { get; }
}
