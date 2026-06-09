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
    private readonly Dictionary<Guid, Anagrafica> _store = new();

    /// <summary>Set di id che il fake dichiarerà "con dipendenze".</summary>
    public HashSet<Guid> DipendenzeDa { get; } = new();

    /// <summary>
    /// Se valorizzata, la prossima Insert/Update/Disattiva lancia
    /// l'eccezione configurata. Permette di simulare le SqlException
    /// senza dipendere da SqlClient reale.
    /// </summary>
    public Exception? ForzaSqlException { get; set; }

    // Solo le attive, come la query reale (WHERE IsAttivo = 1).
    public Task<IReadOnlyList<Anagrafica>> GetAttiviAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Anagrafica>>(
            _store.Values.Where(a => a.IsAttivo).OrderBy(a => a.RagioneSociale).ToList());

    public Task<Anagrafica?> GetByIdAsync(Guid idAnagrafica, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue(idAnagrafica, out var a) ? a : null);

    public Task InsertAsync(Anagrafica anagrafica, CancellationToken cancellationToken = default)
    {
        if (ForzaSqlException is not null)
        {
            var ex = ForzaSqlException;
            ForzaSqlException = null;
            throw ex;
        }
        // L'IdAnagrafica è già valorizzato dal manager (generazione app-side).
        _store[anagrafica.IdAnagrafica] = anagrafica;
        return Task.CompletedTask;
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

    public Task DisattivaAsync(Guid idAnagrafica, CancellationToken cancellationToken = default)
    {
        if (ForzaSqlException is not null)
        {
            var ex = ForzaSqlException;
            ForzaSqlException = null;
            throw ex;
        }
        // Soft-delete: la riga resta nello store ma con IsAttivo = false.
        if (_store.TryGetValue(idAnagrafica, out var a))
        {
            _store[idAnagrafica] = CloneInattiva(a);
        }
        return Task.CompletedTask;
    }

    public Task<bool> HasDipendenzeAsync(Guid idAnagrafica, CancellationToken cancellationToken = default)
        => Task.FromResult(DipendenzeDa.Contains(idAnagrafica));

    // Copia l'Anagrafica con IsAttivo = false. Serve perché l'entità è
    // immutabile sui campi di contenuto (init-only): non possiamo flippare
    // IsAttivo in place.
    private static Anagrafica CloneInattiva(Anagrafica src) => new()
    {
        IdAnagrafica          = src.IdAnagrafica,
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
        IsAttivo              = false,
    };
}
