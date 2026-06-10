namespace ICMFatturazioni.Web.Entities;

/// <summary>
/// Tipo di un <see cref="UtenteToken"/>. Persistito come <c>tinyint</c>
/// (valore esplicito, non l'ordinale implicito) nella colonna <c>Tipo</c>.
/// </summary>
public enum UtenteTokenTipo : byte
{
    /// <summary>Primo accesso: l'utente invitato imposta la password iniziale.</summary>
    Attivazione = 0,

    /// <summary>Password dimenticata: l'utente sceglie una nuova password.</summary>
    Reset = 1,
}
