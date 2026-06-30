using ICMFatturazioni.Web.Entities;

namespace ICMFatturazioni.Web.Repositories.Interfaces;

/// <summary>Accesso ai dati per <c>fatt.Aliquote</c>.</summary>
public interface IAliquotaRepository
{
    /// <summary>Restituisce le aliquote attive, ordinate per descrizione.</summary>
    Task<IReadOnlyList<Aliquota>> GetAttiviAsync(CancellationToken ct = default);

    /// <summary>Restituisce un'aliquota per chiave primaria (incluse soft-deleted).</summary>
    Task<Aliquota?> GetByIdAsync(Guid idAliquota, CancellationToken ct = default);

    /// <summary>Inserisce una nuova aliquota.</summary>
    Task InsertAsync(Aliquota aliquota, CancellationToken ct = default);

    /// <summary>Aggiorna descrizione e valore di un'aliquota esistente.</summary>
    Task UpdateAsync(Aliquota aliquota, CancellationToken ct = default);

    /// <summary>Soft-delete: imposta IsAttivo = 0.</summary>
    Task DisattivaAsync(Guid idAliquota, CancellationToken ct = default);
}
