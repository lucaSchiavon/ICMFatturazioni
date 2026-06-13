namespace ICMFatturazioni.Web.Models;

/// <summary>
/// Vista "ricca" di una banca di appoggio: il legame
/// <c>Entities.BancaAppoggio</c> con i nomi/codici di banca e agenzia già
/// risolti (JOIN su <c>fatt.Banche</c>/<c>fatt.Agenzie</c>). Usata dalla griglia
/// e per pre-popolare il dialog in modifica.
/// </summary>
/// <param name="IdBancaAppoggio">PK del legame.</param>
/// <param name="IdCliente">Cliente intestatario, o <c>null</c> se banca azienda.</param>
/// <param name="IdBanca">PK dell'istituto.</param>
/// <param name="BancaNome">Nome dell'istituto.</param>
/// <param name="ABI">ABI dell'istituto (può essere <c>null</c>).</param>
/// <param name="IdAgenzia">PK della filiale, o <c>null</c> se non indicata.</param>
/// <param name="AgenziaNome">Nome della filiale, o <c>null</c>.</param>
/// <param name="CAB">CAB della filiale, o <c>null</c>.</param>
/// <param name="IBAN">IBAN (solo banche azienda), o <c>null</c>.</param>
/// <param name="IsAttivo">Soft-delete.</param>
public sealed record BancaAppoggioRiga(
    Guid IdBancaAppoggio,
    Guid? IdCliente,
    Guid IdBanca,
    string BancaNome,
    string? ABI,
    Guid? IdAgenzia,
    string? AgenziaNome,
    string? CAB,
    string? IBAN,
    bool IsAttivo)
{
    /// <summary>Stato derivato: <c>true</c> se è una banca dell'Azienda.</summary>
    public bool IsBancaAzienda => IdCliente is null;
}
