using ICMFatturazioni.Web.Managers;

namespace ICMFatturazioni.Web.Authentication;

/// <summary>
/// Messaggi user-friendly per i motivi di invalidità di un magic-link.
/// Centralizzati qui perché usati sia dagli endpoint (Program.cs) sia dalle
/// pagine <c>/attiva</c> e <c>/reset-password</c>. Niente dettagli tecnici
/// (date/tipi): solo l'azione successiva utile all'utente.
/// </summary>
public static class UtenteTokenMessaggi
{
    public static string Messaggio(UtenteTokenInvalidoMotivo motivo) => motivo switch
    {
        UtenteTokenInvalidoMotivo.NonTrovato =>
            "Link non valido. Verifica di aver copiato l'indirizzo completo oppure richiedine uno nuovo.",
        UtenteTokenInvalidoMotivo.Revocato =>
            "Questo link è stato sostituito da uno più recente. Usa l'ultimo link ricevuto o richiedine uno nuovo.",
        UtenteTokenInvalidoMotivo.GiaUsato =>
            "Questo link è già stato utilizzato. Se devi cambiare la password, richiedine uno nuovo.",
        UtenteTokenInvalidoMotivo.Scaduto =>
            "Questo link è scaduto. Richiedine uno nuovo.",
        _ => "Link non valido.",
    };

    public static string Titolo(UtenteTokenInvalidoMotivo motivo) => motivo switch
    {
        UtenteTokenInvalidoMotivo.Revocato => "Link sostituito",
        UtenteTokenInvalidoMotivo.GiaUsato => "Link già usato",
        UtenteTokenInvalidoMotivo.Scaduto => "Link scaduto",
        _ => "Link non valido",
    };
}
