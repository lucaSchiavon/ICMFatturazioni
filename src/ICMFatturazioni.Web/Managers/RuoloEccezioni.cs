namespace ICMFatturazioni.Web.Managers;

/// <summary>Username/nome ruolo già esistente.</summary>
public sealed class RuoloDuplicatoException : Exception
{
    public RuoloDuplicatoException(string nome)
        : base($"Esiste già un ruolo con nome «{nome}».") => Nome = nome;

    public string Nome { get; }
}

/// <summary>
/// Tentativo di modificare/eliminare un ruolo di SISTEMA (Superadmin/Admin),
/// che è protetto: non rinominabile né eliminabile dalla UI.
/// </summary>
public sealed class RuoloProtettoException : Exception
{
    public RuoloProtettoException(string nome)
        : base($"Il ruolo di sistema «{nome}» non può essere modificato o eliminato.") => Nome = nome;

    public string Nome { get; }
}

/// <summary>
/// Tentativo di eliminare un ruolo ancora assegnato a uno o più utenti:
/// va prima riassegnato.
/// </summary>
public sealed class RuoloInUsoException : Exception
{
    public RuoloInUsoException(string nome, int numeroUtenti)
        : base($"Il ruolo «{nome}» è assegnato a {numeroUtenti} utente/i: riassegnali prima di eliminarlo.")
    {
        Nome = nome;
        NumeroUtenti = numeroUtenti;
    }

    public string Nome { get; }
    public int NumeroUtenti { get; }
}
