using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// In-memory repository per testare <c>CantiereManager</c> senza DB.
/// Volutamente "stupido": non valida FK, non simula concorrenza.
/// <see cref="ForzaSqlException"/> permette di verificare i branch di
/// traduzione delle violazioni di vincolo (doppia difesa).
/// </summary>
internal sealed class FakeCantiereRepository : ICantiereRepository
{
    private readonly Dictionary<Guid, Cantiere> _store = new();

    /// <summary>
    /// Se valorizzata, la prossima Insert/Update lancia l'eccezione configurata.
    /// Permette di simulare le SqlException senza dipendere da SqlClient reale.
    /// </summary>
    public Exception? ForzaSqlException { get; set; }

    // Solo gli attivi, come la query reale (WHERE IsAttivo = 1).
    public Task<IReadOnlyList<Cantiere>> GetAttiviAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Cantiere>>(
            _store.Values.Where(c => c.IsAttivo).OrderBy(c => c.Ubicazione).ToList());

    public Task<Cantiere?> GetByIdAsync(Guid idCantiere, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue(idCantiere, out var c) ? c : null);

    public Task InsertAsync(Cantiere cantiere, CancellationToken cancellationToken = default)
    {
        LanciaSeForzata();
        // L'IdCantiere è già valorizzato dal manager (generazione app-side).
        _store[cantiere.IdCantiere] = cantiere;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Cantiere cantiere, CancellationToken cancellationToken = default)
    {
        LanciaSeForzata();
        _store[cantiere.IdCantiere] = cantiere;
        return Task.CompletedTask;
    }

    public Task DisattivaAsync(Guid idCantiere, CancellationToken cancellationToken = default)
    {
        LanciaSeForzata();
        // Soft-delete: la riga resta nello store ma con IsAttivo = false.
        if (_store.TryGetValue(idCantiere, out var c))
        {
            c.IsAttivo = false;
        }
        return Task.CompletedTask;
    }

    private void LanciaSeForzata()
    {
        if (ForzaSqlException is not null)
        {
            var ex = ForzaSqlException;
            ForzaSqlException = null;
            throw ex;
        }
    }
}
