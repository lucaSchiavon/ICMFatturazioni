using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Web.Managers.Interfaces;

public interface IAttivitaConsulentePagamentoManager
{
    /// <summary>Righe consulenza a carico Studio di un'attività, con Pagato/Residuo derivati.</summary>
    Task<IReadOnlyList<ConsulenzaConSaldo>> ConsulenzeConSaldoAsync(Guid idAttivita, CancellationToken cancellationToken = default);

    /// <summary>Tranche attive di una riga consulenza, ordinate per data.</summary>
    Task<IReadOnlyList<AttivitaConsulentePagamento>> ElencoPerRigaAsync(Guid idAttivitaConsulente, CancellationToken cancellationToken = default);

    /// <summary>Registra una tranche. Bloccata oltre il residuo (D-C3) e su righe non a carico Studio.</summary>
    Task<Guid> CreaAsync(AttivitaConsulentePagamento pagamento, CancellationToken cancellationToken = default);

    /// <summary>Modifica data/importo/nota di una tranche. Il residuo non può diventare negativo (D-C3).</summary>
    Task AggiornaAsync(AttivitaConsulentePagamento pagamento, CancellationToken cancellationToken = default);

    /// <summary>Soft-delete della tranche (il residuo della riga risale).</summary>
    Task EliminaAsync(Guid idConsulentePagamento, CancellationToken cancellationToken = default);
}
