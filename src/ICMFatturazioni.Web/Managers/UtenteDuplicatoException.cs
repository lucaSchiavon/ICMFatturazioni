namespace ICMFatturazioni.Web.Managers;

/// <summary>
/// Sollevata quando si tenta di creare/modificare un utente con uno
/// <c>Username</c> già esistente. Eccezione di validazione tipizzata: porta un
/// messaggio user-friendly e NON va loggata (vedi CLAUDE.md Regola 6).
/// </summary>
public sealed class UtenteDuplicatoException : Exception
{
    public UtenteDuplicatoException(string username)
        : base($"Esiste già un utente con username «{username}».")
    {
        Username = username;
    }

    public string Username { get; }
}
