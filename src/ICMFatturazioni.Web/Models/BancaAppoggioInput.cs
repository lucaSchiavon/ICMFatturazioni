namespace ICMFatturazioni.Web.Models;

/// <summary>
/// Comando di salvataggio di una banca di appoggio, così come arriva dal dialog.
/// Lavora con i <b>nomi</b> di banca e agenzia (non con gli id): il manager li
/// risolve in anagrafiche (<c>fatt.Banche</c>/<c>fatt.Agenzie</c>) con logica
/// "get-or-create" e, se l'utente modifica ABI/CAB di una banca/agenzia
/// esistente, li <b>aggiorna</b> (niente doppioni divergenti).
/// </summary>
/// <param name="IdBancaAppoggio"><see cref="System.Guid.Empty"/> in creazione, valorizzato in modifica.</param>
/// <param name="IdCliente">Cliente intestatario, o <c>null</c> per la banca azienda.</param>
/// <param name="BancaNome">Nome dell'istituto (obbligatorio).</param>
/// <param name="ABI">ABI dell'istituto (5 cifre), o <c>null</c>.</param>
/// <param name="AgenziaNome">Nome della filiale, o <c>null</c> se non indicata.</param>
/// <param name="CAB">CAB della filiale (5 cifre), o <c>null</c>.</param>
/// <param name="IBAN">IBAN (solo banca azienda), o <c>null</c>.</param>
public sealed record BancaAppoggioInput(
    Guid IdBancaAppoggio,
    Guid? IdCliente,
    string BancaNome,
    string? ABI,
    string? AgenziaNome,
    string? CAB,
    string? IBAN);
