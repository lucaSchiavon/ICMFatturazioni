using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Tests.Managers;

// =============================================================================
// Doppi in-memory dei manager dei cataloghi referenziati da Anagrafica.
// Servono ai test di AnagraficaManager per la validazione dei riferimenti
// (esistenza/IsAttivo) e della coerenza FlagBanca↔banca. Solo i metodi usati
// dal manager sono implementati; il resto lancia NotImplementedException.
// =============================================================================

/// <summary>Fake di <see cref="ICodicePagamentoManager"/>: espone solo Elenco.</summary>
internal sealed class FakeCodicePagamentoManager : ICodicePagamentoManager
{
    public List<CodicePagamentoRiga> Righe { get; } = new();

    /// <summary>Aggiunge una riga di test con il solo flag banca rilevante e ritorna l'Id.</summary>
    public Guid Aggiungi(FlagBanca flag)
    {
        var id = Guid.NewGuid();
        Righe.Add(new CodicePagamentoRiga(
            IdCodicePagamento: id,
            IdTipoPagamento: Guid.NewGuid(),
            TipoDescrizione: "Tipo",
            FlagBanca: flag,
            DescrPag: "Pagamento di test",
            NumScadenze: 1,
            GGScad1: 0,
            GGScad2: null,
            GGScad3: null,
            GGpiu: null,
            FineMese: false,
            CondizionePagamento: null,
            CondizioneDescrizione: null,
            ModalitaPagamento: null,
            ModalitaDescrizione: null,
            IsAttivo: true));
        return id;
    }

    public Task<IReadOnlyList<CodicePagamentoRiga>> ElencoAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CodicePagamentoRiga>>(Righe);

    public Task<CodicePagamento?> GetByIdAsync(Guid idCodicePagamento, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
    public Task<Guid> CreaAsync(CodicePagamento codice, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
    public Task AggiornaAsync(CodicePagamento codice, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
    public Task EliminaAsync(Guid idCodicePagamento, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
    public Task<bool> EEliminabileAsync(Guid idCodicePagamento, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}

/// <summary>Fake di <see cref="IBancaAppoggioManager"/>: espone solo GetById.</summary>
internal sealed class FakeBancaAppoggioManager : IBancaAppoggioManager
{
    public Dictionary<Guid, BancaAppoggioRiga> Banche { get; } = new();

    /// <summary>Aggiunge una banca aziendale (IdCliente null) e ritorna l'Id.</summary>
    public Guid AggiungiAzienda(bool attiva = true) => Aggiungi(idCliente: null, attiva);

    /// <summary>Aggiunge una banca del cliente indicato e ritorna l'Id.</summary>
    public Guid AggiungiCliente(Guid idCliente, bool attiva = true) => Aggiungi(idCliente, attiva);

    private Guid Aggiungi(Guid? idCliente, bool attiva)
    {
        var id = Guid.NewGuid();
        Banche[id] = new BancaAppoggioRiga(
            IdBancaAppoggio: id,
            IdCliente: idCliente,
            IdBanca: Guid.NewGuid(),
            BancaNome: "Banca di test",
            ABI: "01234",
            IdAgenzia: null,
            AgenziaNome: null,
            CAB: null,
            IBAN: null,
            IsAttivo: attiva);
        return id;
    }

    public Task<BancaAppoggioRiga?> GetByIdAsync(Guid idBancaAppoggio, CancellationToken cancellationToken = default)
        => Task.FromResult(Banche.TryGetValue(idBancaAppoggio, out var b) ? b : null);

    public Task<IReadOnlyList<BancaAppoggioRiga>> ElencoAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<BancaAppoggioRiga>>(Banche.Values.ToList());

    public Task<IReadOnlyList<BancaAppoggioRiga>> SelezionabiliAsync(Guid? idCliente, bool bancheAzienda, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
    public Task<Guid> CreaAsync(BancaAppoggioInput input, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
    public Task AggiornaAsync(BancaAppoggioInput input, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
    public Task EliminaAsync(Guid idBancaAppoggio, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
    public Task<bool> EEliminabileAsync(Guid idBancaAppoggio, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}

/// <summary>Fake di <see cref="ICodiceIVAManager"/>: espone solo GetById.</summary>
internal sealed class FakeCodiceIVAManager : ICodiceIVAManager
{
    public Dictionary<Guid, CodiceIVA> Codici { get; } = new();

    /// <summary>Aggiunge un codice IVA (eventualmente disattivato) e ritorna l'Id.</summary>
    public Guid Aggiungi(bool attivo = true)
    {
        var id = Guid.NewGuid();
        Codici[id] = new CodiceIVA
        {
            IdCodiceIVA = id,
            Codice = "22",
            Descrizione = "IVA 22%",
            Aliquota = 22m,
            IsAttivo = attivo,
        };
        return id;
    }

    public Task<CodiceIVA?> GetByIdAsync(Guid idCodiceIVA, CancellationToken cancellationToken = default)
        => Task.FromResult(Codici.TryGetValue(idCodiceIVA, out var c) ? c : null);

    public Task<IReadOnlyList<CodiceIVA>> ElencoAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CodiceIVA>>(Codici.Values.ToList());

    public Task<Guid> CreaAsync(CodiceIVA codiceIva, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
    public Task AggiornaAsync(CodiceIVA codiceIva, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
    public Task EliminaAsync(Guid idCodiceIVA, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
    public Task<bool> EEliminabileAsync(Guid idCodiceIVA, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
