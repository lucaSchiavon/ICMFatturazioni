using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Web.Repositories.Interfaces;

public interface IAttivitaConsulentePagamentoRepository
{
    /// <summary>
    /// Righe consulenza A CARICO DELLO STUDIO di un'attività, con Pagato derivato
    /// dalle tranche attive (read-model della maschera pagamenti, dispensa cap. 5).
    /// </summary>
    Task<IReadOnlyList<ConsulenzaConSaldo>> GetConsulenzeConSaldoAsync(Guid idAttivita, CancellationToken cancellationToken = default);

    /// <summary>Tranche attive di una riga consulenza, ordinate per data.</summary>
    Task<IReadOnlyList<AttivitaConsulentePagamento>> GetByRigaAsync(Guid idAttivitaConsulente, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tranche attive delle righe consulenza attive di un consulente (null = tutti),
    /// ordinate per data: alimentano il dettaglio pagamenti del report (dispensa cap. 7).
    /// </summary>
    Task<IReadOnlyList<AttivitaConsulentePagamento>> GetByConsulenteAsync(Guid? idConsulente, CancellationToken cancellationToken = default);

    Task<AttivitaConsulentePagamento?> GetByIdAsync(Guid idConsulentePagamento, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saldo della riga per la guardia D-C3: importo dovuto, pagato (escludendo
    /// eventualmente la tranche in modifica) e flag carico Studio.
    /// Null se la riga non esiste o non è attiva.
    /// </summary>
    Task<SaldoRiga?> GetSaldoRigaAsync(Guid idAttivitaConsulente, Guid? escludiPagamento, CancellationToken cancellationToken = default);

    Task InsertAsync(AttivitaConsulentePagamento pagamento, CancellationToken cancellationToken = default);
    Task UpdateAsync(AttivitaConsulentePagamento pagamento, CancellationToken cancellationToken = default);
    Task DisattivaAsync(Guid idConsulentePagamento, CancellationToken cancellationToken = default);
}
