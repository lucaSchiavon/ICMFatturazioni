using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// In-memory repository per testare <c>CodiceIVAManager</c> senza DB.
/// Replica la semantica delle query reali rilevanti per i test: solo attivi in
/// elenco, unicità del codice tra gli attivi (case-insensitive), soft-delete,
/// dipendenze pilotabili via <see cref="DipendenzeDa"/>.
/// </summary>
internal sealed class FakeCodiceIVARepository : ICodiceIVARepository
{
    private readonly Dictionary<Guid, CodiceIVA> _store = new();

    /// <summary>Set di id che il fake dichiarerà "con dipendenze".</summary>
    public HashSet<Guid> DipendenzeDa { get; } = new();

    public Task<IReadOnlyList<CodiceIVA>> GetAttiviAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CodiceIVA>>(
            _store.Values.Where(c => c.IsAttivo).OrderBy(c => c.Codice, StringComparer.OrdinalIgnoreCase).ToList());

    public Task<CodiceIVA?> GetByIdAsync(Guid idCodiceIVA, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue(idCodiceIVA, out var c) ? c : null);

    public Task<bool> ExistsCodiceAttivoAsync(string codice, Guid? escludiId, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.Values.Any(c =>
            c.IsAttivo
            && string.Equals(c.Codice, codice, StringComparison.OrdinalIgnoreCase)
            && (escludiId is null || c.IdCodiceIVA != escludiId)));

    public Task InsertAsync(CodiceIVA codiceIva, CancellationToken cancellationToken = default)
    {
        // L'IdCodiceIVA è già valorizzato dal manager (generazione app-side).
        _store[codiceIva.IdCodiceIVA] = codiceIva;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(CodiceIVA codiceIva, CancellationToken cancellationToken = default)
    {
        _store[codiceIva.IdCodiceIVA] = codiceIva;
        return Task.CompletedTask;
    }

    public Task DisattivaAsync(Guid idCodiceIVA, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(idCodiceIVA, out var c))
        {
            _store[idCodiceIVA] = CloneInattivo(c);
        }
        return Task.CompletedTask;
    }

    public Task<bool> HasDipendenzeAsync(Guid idCodiceIVA, CancellationToken cancellationToken = default)
        => Task.FromResult(DipendenzeDa.Contains(idCodiceIVA));

    // Copia il CodiceIVA con IsAttivo = false (entità immutabile sui campi init-only).
    private static CodiceIVA CloneInattivo(CodiceIVA src) => new()
    {
        IdCodiceIVA  = src.IdCodiceIVA,
        Codice       = src.Codice,
        Descrizione  = src.Descrizione,
        Aliquota     = src.Aliquota,
        Natura       = src.Natura,
        ObbligoBollo = src.ObbligoBollo,
        IsAttivo     = false,
    };
}
