using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Web.Repositories.Interfaces;

/// <summary>
/// Accesso ai dati per <c>fatt.Fatture</c>. Tabella semplice (single-table): la
/// fattura è il "certificato di fatturazione" di un avviso, senza figli propri —
/// righe, spese e snapshot fiscali restano sull'avviso di origine.
///
/// Soft-delete: <see cref="AnnullaAsync"/> imposta <c>IsAttivo = 0</c> liberando gli
/// indici univoci filtrati (l'avviso torna fatturabile, il numero riutilizzabile).
/// </summary>
public interface IFattureRepository
{
    /// <summary>Restituisce una fattura per chiave primaria (anche se annullata).</summary>
    Task<Fattura?> GetByIdAsync(Guid idFattura, CancellationToken ct = default);

    /// <summary>Restituisce la fattura ATTIVA di un avviso, o null se non fatturato.</summary>
    Task<Fattura?> GetAttivaByAvvisoAsync(Guid idAvviso, CancellationToken ct = default);

    /// <summary>
    /// Numero massimo di fattura ATTIVA per l'anno indicato, o 0 se l'anno è vuoto.
    /// Il numero proposto = questo + 1 (calcolo app-side, come per i GUID).
    /// </summary>
    Task<int> GetMaxNumeroAnnoAsync(int anno, CancellationToken ct = default);

    /// <summary>
    /// Inserisce la fattura (PK/Anno già assegnati app-side).
    /// </summary>
    /// <exception cref="Managers.FatturaInvalidaException">
    /// Con motivo <c>AvvisoGiaFatturato</c> se l'avviso ha già una fattura attiva, o
    /// <c>NumeroDuplicato</c> se il numero è già usato nell'anno (indici univoci filtrati).
    /// </exception>
    Task CreateAsync(Fattura fattura, CancellationToken ct = default);

    /// <summary>Soft-delete della fattura: <c>IsAttivo = 0</c>. Idempotente.</summary>
    Task AnnullaAsync(Guid idFattura, CancellationToken ct = default);

    /// <summary>
    /// Fatture ATTIVE di un'attività (join sull'avviso di origine → attività/cliente/
    /// tipo), ordinate per anno e numero decrescenti. Alimenta la griglia "Stampe fatture".
    /// </summary>
    Task<IReadOnlyList<FatturaEmessa>> GetEmesseByAttivitaAsync(Guid idAttivita, CancellationToken ct = default);

    /// <summary>
    /// Anni per cui esiste almeno una fattura ATTIVA, decrescenti. Popola la combo
    /// "Fatture emesse anno" (elenco globale, non ristretto al cliente selezionato).
    /// </summary>
    Task<IReadOnlyList<int>> GetAnniConFattureAsync(CancellationToken ct = default);

    /// <summary>
    /// Coppie (cliente, attività) con almeno una fattura ATTIVA. Restringe i selettori
    /// della maschera "Stampe fatture" ai soli clienti/attività che hanno fatture.
    /// </summary>
    Task<IReadOnlyList<AttivitaFatturabile>> GetAttivitaConFattureAsync(CancellationToken ct = default);
}
