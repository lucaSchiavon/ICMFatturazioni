using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Repositories.Interfaces;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// In-memory repository per testare <c>AnagraficaManager</c> senza DB.
/// Volutamente "stupido" e di test: non valida FK, non simula concorrenza.
/// Espone hook (<see cref="DipendenzeDa"/>, <see cref="ForzaSqlException"/>)
/// per i test che devono verificare branch di errore specifici.
/// </summary>
internal sealed class FakeAnagraficaRepository : IAnagraficaRepository
{
    private readonly Dictionary<int, Anagrafica> _store = new();
    private int _nextId = 1;

    /// <summary>Set di id che il fake dichiarerà "con dipendenze".</summary>
    public HashSet<int> DipendenzeDa { get; } = new();

    /// <summary>
    /// Se valorizzata, la prossima Insert/Update/Delete lancia
    /// l'eccezione configurata. Permette di simulare le SqlException
    /// senza dipendere da SqlClient reale.
    /// </summary>
    public Exception? ForzaSqlException { get; set; }

    public Task<IReadOnlyList<Anagrafica>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Anagrafica>>(
            _store.Values.OrderBy(a => a.RagioneSociale).ToList());

    public Task<Anagrafica?> GetByIdAsync(int idAnagrafica, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue(idAnagrafica, out var a) ? a : null);

    public Task<int> InsertAsync(Anagrafica anagrafica, CancellationToken cancellationToken = default)
    {
        if (ForzaSqlException is not null)
        {
            var ex = ForzaSqlException;
            ForzaSqlException = null;
            throw ex;
        }
        var id = _nextId++;
        var withId = CloneWith(anagrafica, id);
        _store[id] = withId;
        return Task.FromResult(id);
    }

    public Task UpdateAsync(Anagrafica anagrafica, CancellationToken cancellationToken = default)
    {
        if (ForzaSqlException is not null)
        {
            var ex = ForzaSqlException;
            ForzaSqlException = null;
            throw ex;
        }
        _store[anagrafica.IdAnagrafica] = anagrafica;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(int idAnagrafica, CancellationToken cancellationToken = default)
    {
        if (ForzaSqlException is not null)
        {
            var ex = ForzaSqlException;
            ForzaSqlException = null;
            throw ex;
        }
        _store.Remove(idAnagrafica);
        return Task.CompletedTask;
    }

    public Task<bool> HasDipendenzeAsync(int idAnagrafica, CancellationToken cancellationToken = default)
        => Task.FromResult(DipendenzeDa.Contains(idAnagrafica));

    // Crea una copia dell'Anagrafica con l'id specificato. Serve perché
    // Anagrafica è immutabile (init-only): non possiamo settare l'id
    // dopo costruzione.
    private static Anagrafica CloneWith(Anagrafica src, int id) => new()
    {
        IdAnagrafica          = id,
        TipoAnagrafica        = src.TipoAnagrafica,
        RagioneSociale        = src.RagioneSociale,
        Indirizzo             = src.Indirizzo,
        CAP                   = src.CAP,
        City                  = src.City,
        Provincia             = src.Provincia,
        SiglaPaese            = src.SiglaPaese,
        Telefono              = src.Telefono,
        Cellulare             = src.Cellulare,
        Fax                   = src.Fax,
        Email                 = src.Email,
        PIVA                  = src.PIVA,
        Contatto              = src.Contatto,
        IdPag                 = src.IdPag,
        IdBancaAppoggio       = src.IdBancaAppoggio,
        IdCodiciIVA           = src.IdCodiciIVA,
        IdTipologieClientela  = src.IdTipologieClientela,
        CodiceDestinatario    = src.CodiceDestinatario,
        PECFatturaElettronica = src.PECFatturaElettronica,
        DataRecord            = src.DataRecord,
    };
}
