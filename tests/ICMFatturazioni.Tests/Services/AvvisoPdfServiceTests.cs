using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Models;
using ICMFatturazioni.Web.Services;

namespace ICMFatturazioni.Tests.Services;

/// <summary>
/// Test del PDF dell'Avviso di fattura. Filosofia (come VerbalePdfServiceTests di
/// ICMVerbali): niente Moq, fake espliciti; la qualità visiva del layout non è
/// testabile a costo ragionevole, quindi si verifica che il PDF si generi (magic
/// header <c>%PDF</c>) coprendo i rami di rendering, più le guardie del servizio.
/// </summary>
public class AvvisoPdfServiceTests
{
    // Vera cascata fiscale (servizio puro), come fa AvvisoFatturaManager.Calcola.
    private static readonly CalcoloFiscaleAvviso _calcolo = new();

    private static CalcoloFiscaleRisultato Calcola(AvvisoFattura a, decimal imponibile, decimal spese)
        => _calcolo.Calcola(new CalcoloFiscaleInput(
            imponibile, a.AliquotaCnpaia, a.AliquotaIva, a.AliquotaRitenuta,
            ApplicaCassa: true, ApplicaRitenuta: a.ApplicaRitenuta, SpeseArt15: spese));

    // -------------------------------------------------------------------------
    // Rendering diretto del documento (nessun manager necessario)
    // -------------------------------------------------------------------------

    [Fact]
    public void Render_ConDatiCompleti_ProducePdf()
    {
        var data = CostruisciData(applicaRitenuta: false, spese: 0m, includiDescrittiva: true);

        var pdf = new AvvisoPdfDocument(data).Render();

        AssertPdfValido(pdf);
    }

    [Fact]
    public void Render_ConRitenutaESpese_ProducePdf()
    {
        // Copre i rami condizionali della cascata (riga ritenuta + riga spese art.15).
        var data = CostruisciData(applicaRitenuta: true, spese: 300m, includiDescrittiva: false);

        var pdf = new AvvisoPdfDocument(data).Render();

        AssertPdfValido(pdf);
    }

    [Fact]
    public void Render_SoloRigheDescrittive_ProducePdf()
    {
        var testata = CostruisciTestata(applicaRitenuta: false);
        var righe = new List<AvvisoFatturaRiga>
        {
            new() { IdRiga = Guid.NewGuid(), IdAvviso = testata.IdAvviso, Ordine = 1,
                    Descrizione = "Solo testo, nessun importo", IsDescrittiva = true },
        };
        var data = new AvvisoPdfData(
            CostruisciStudio(), CostruisciCliente(), CostruisciAttivita(), testata, righe,
            "A VISTA", "Banca Esempio - IBAN: IT00X0000000000000000000000",
            Calcola(testata, 0m, 0m));

        var pdf = new AvvisoPdfDocument(data).Render();

        AssertPdfValido(pdf);
    }

    [Fact]
    public void Render_DatiAziendaEClienteParziali_NonVaInErrore()
    {
        // Anagrafica minimale (molti campi null): il documento non deve crashare.
        var studio = new Azienda { NomeBreve = "S", RagioneSociale = "Studio Minimo" };
        var cliente = new Anagrafica { TipoAnagrafica = TipoAnagrafica.Privato, RagioneSociale = "Mario Rossi" };
        var testata = CostruisciTestata(applicaRitenuta: false);
        var righe = new List<AvvisoFatturaRiga>
        {
            new() { IdRiga = Guid.NewGuid(), IdAvviso = testata.IdAvviso, Ordine = 1,
                    Tipo = "Prestazione", Descrizione = "Attività", Importo = 1000m, IsDescrittiva = false },
        };
        var data = new AvvisoPdfData(studio, cliente, null, testata, righe, null, null,
            Calcola(testata, 1000m, 0m));

        var pdf = new AvvisoPdfDocument(data).Render();

        AssertPdfValido(pdf);
    }

    [Fact]
    public void Render_ModalitaFattura_ProducePdf()
    {
        // Documento reso come FATTURA: copre la barra "FATTURA" (numero/data fattura),
        // il banner "non valido ai fini fiscali" e il footer "Riferimento Avviso del…".
        var testata = CostruisciTestata(applicaRitenuta: true);
        var righe = new List<AvvisoFatturaRiga>
        {
            new() { IdRiga = Guid.NewGuid(), IdAvviso = testata.IdAvviso, Ordine = 1,
                    Tipo = "det1", Descrizione = "det1", Importo = 1000m, IsDescrittiva = false },
        };
        var fattura = new Fattura
        {
            IdFattura = Guid.NewGuid(), IdAvviso = testata.IdAvviso,
            NumeroFattura = 37, Anno = 2026, DataFattura = new DateOnly(2026, 7, 30), IsAttivo = true,
        };
        var data = new AvvisoPdfData(
            CostruisciStudio(), CostruisciCliente(), CostruisciAttivita(), testata, righe,
            "A VISTA", "Banca Esempio - IBAN: IT00X0000000000000000000000",
            Calcola(testata, 1000m, 300m), fattura);

        var pdf = new AvvisoPdfDocument(data).Render();

        AssertPdfValido(pdf);
    }

    // -------------------------------------------------------------------------
    // Servizio: guardie e happy path
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GeneraAsync_AvvisoInesistente_LanciaNonTrovato()
    {
        var avvisi = new FakeAvvisoManager { Dettaglio = null };
        var service = CostruisciService(avvisi);

        await Assert.ThrowsAsync<AvvisoPdfNonTrovatoException>(
            () => service.GeneraAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GeneraAsync_AvvisoAnnullato_LanciaNonTrovato()
    {
        var testata = CostruisciTestata(applicaRitenuta: false, isAttivo: false);   // soft-deleted
        var avvisi = new FakeAvvisoManager
        {
            Dettaglio = new AvvisoDettaglio(testata, Array.Empty<AvvisoFatturaRiga>()),
        };
        var service = CostruisciService(avvisi);

        await Assert.ThrowsAsync<AvvisoPdfNonTrovatoException>(
            () => service.GeneraAsync(testata.IdAvviso));
    }

    [Fact]
    public async Task GeneraAsync_AziendaNonConfigurata_LanciaDatiMancanti()
    {
        var testata = CostruisciTestata(applicaRitenuta: false);
        var avvisi = new FakeAvvisoManager
        {
            Dettaglio = new AvvisoDettaglio(testata, Array.Empty<AvvisoFatturaRiga>()),
        };
        var service = CostruisciService(avvisi, azienda: null);   // GetAziendaAsync → null

        await Assert.ThrowsAsync<AvvisoPdfDatiMancantiException>(
            () => service.GeneraAsync(testata.IdAvviso));
    }

    [Fact]
    public async Task GeneraAsync_HappyPath_ProducePdf()
    {
        var testata = CostruisciTestata(applicaRitenuta: true);
        var righe = new List<AvvisoFatturaRiga>
        {
            new() { IdRiga = Guid.NewGuid(), IdAvviso = testata.IdAvviso, Ordine = 1,
                    Tipo = "det1", Descrizione = "det1", Importo = 1000m, IsDescrittiva = false },
            new() { IdRiga = Guid.NewGuid(), IdAvviso = testata.IdAvviso, Ordine = 2,
                    Tipo = "eee", Descrizione = "eee", Importo = 2000m, IsDescrittiva = false },
        };
        var avvisi = new FakeAvvisoManager { Dettaglio = new AvvisoDettaglio(testata, righe) };
        var service = CostruisciService(avvisi);

        var pdf = await service.GeneraAsync(testata.IdAvviso);

        AssertPdfValido(pdf);
    }

    // -------------------------------------------------------------------------
    // Helper di costruzione dati (riflettono il modello docs/AvvisoDiFattura.pdf)
    // -------------------------------------------------------------------------

    private static void AssertPdfValido(byte[] pdf)
    {
        Assert.True(pdf.Length > 0);
        Assert.Equal((byte)'%', pdf[0]);
        Assert.Equal((byte)'P', pdf[1]);
        Assert.Equal((byte)'D', pdf[2]);
        Assert.Equal((byte)'F', pdf[3]);
    }

    private static Azienda CostruisciStudio() => new()
    {
        NomeBreve = "Bortolaso Vantini",
        RagioneSociale = "Vantini Oliosi Nardon Architetti",
        PIVA = "01961500236",
        CodiceFiscale = "01961500236",
        IndirizzoVia = "Via Crivelin",
        IndirizzoCivico = "7",
        IndirizzoComune = "Affi",
        IndirizzoProvincia = "VR",
        IndirizzoCAP = "37010",
        IndirizzoPaese = "IT",
        Telefono = "045 8030328",
        Telefax = "045 8013096",
        Email = "bv.archh@bortolasovantini.it",
        PEC = "bortolasovantini@pecsicura.it",
        RegimeFiscale = "RF01",
    };

    private static Anagrafica CostruisciCliente() => new()
    {
        TipoAnagrafica = TipoAnagrafica.Societa,
        RagioneSociale = "3 ESSE S.A.S. DI SALETTI CARLO & C.",
        Indirizzo = "VIA G. ROSSINI, 1",
        CAP = "37017",
        City = "LAZISE",
        Provincia = "VR",
        PIVA = "02321940237",
    };

    private static Attivita CostruisciAttivita() => new()
    {
        IdAttivita = Guid.NewGuid(),
        Numero = "678",
        Descrizione = "attx",
    };

    private static AvvisoFattura CostruisciTestata(bool applicaRitenuta, bool isAttivo = true) => new()
    {
        IdAvviso = Guid.NewGuid(),
        IdAttivita = Guid.NewGuid(),
        IdAnagrafica = Guid.NewGuid(),
        DataAvviso = new DateOnly(2026, 7, 16),
        Oggetto = "attx",
        NotaSintetica = "avviso di prova",
        AliquotaIva = 22m,
        AliquotaCnpaia = 4m,
        AliquotaRitenuta = 20m,
        ApplicaRitenuta = applicaRitenuta,
        DescrizioneSpeseInAvviso = applicaRitenuta ? "Bolli e diritti anticipati" : null,
        IsAttivo = isAttivo,
    };

    private static AvvisoPdfData CostruisciData(bool applicaRitenuta, decimal spese, bool includiDescrittiva)
    {
        var testata = CostruisciTestata(applicaRitenuta);
        var righe = new List<AvvisoFatturaRiga>();
        if (includiDescrittiva)
            righe.Add(new AvvisoFatturaRiga
            {
                IdRiga = Guid.NewGuid(), IdAvviso = testata.IdAvviso, Ordine = 1,
                Descrizione = "descrizione", IsDescrittiva = true,
            });
        righe.Add(new AvvisoFatturaRiga
        {
            IdRiga = Guid.NewGuid(), IdAvviso = testata.IdAvviso, Ordine = 2,
            Tipo = "det1", Descrizione = "det1", Importo = 1000m, IsDescrittiva = false,
        });
        righe.Add(new AvvisoFatturaRiga
        {
            IdRiga = Guid.NewGuid(), IdAvviso = testata.IdAvviso, Ordine = 3,
            Tipo = "eee", Descrizione = "eee", Importo = 2000m, IsDescrittiva = false,
        });

        return new AvvisoPdfData(
            CostruisciStudio(), CostruisciCliente(), CostruisciAttivita(), testata, righe,
            "A VISTA",
            "Banco Popolare di Verona e Novara - Piazza Erbe \"B\" - IBAN: IT74H0503411750000000003403",
            Calcola(testata, 3000m, spese));
    }

    private static AvvisoPdfService CostruisciService(FakeAvvisoManager avvisi, bool azienda = true)
        => new(CostruisciBuilder(avvisi, azienda ? CostruisciStudio() : null));

    // Overload per il test "azienda nulla".
    private static AvvisoPdfService CostruisciService(FakeAvvisoManager avvisi, Azienda? azienda)
        => new(CostruisciBuilder(avvisi, azienda));

    // Builder condiviso: assembla i dati dai fake dei 7 manager.
    private static AvvisoPdfDataBuilder CostruisciBuilder(FakeAvvisoManager avvisi, Azienda? azienda)
        => new(
            avvisi,
            new FakeAnagraficaManager(CostruisciCliente()),
            new FakeAttivitaManager(CostruisciAttivita()),
            new FakeCodicePagamentoManager(),
            new FakeBancaAppoggioManager(),
            new FakeSpesaAnticipataManager(),
            new FakeAziendaManager(azienda));

    // -------------------------------------------------------------------------
    // Fake minimali dei manager (implementano solo i metodi chiamati dal servizio)
    // -------------------------------------------------------------------------

    private sealed class FakeAvvisoManager : IAvvisoFatturaManager
    {
        public AvvisoDettaglio? Dettaglio { get; set; }

        public Task<AvvisoDettaglio?> GetDettaglioAsync(Guid idAvviso, CancellationToken ct = default)
            => Task.FromResult(Dettaglio);

        public Task<IReadOnlyList<DettaglioAvvisoGrandezze>> DettagliGrandezzeAsync(Guid idAvviso, CancellationToken ct = default) => throw new NotImplementedException();

        public CalcoloFiscaleRisultato Calcola(AvvisoFattura avviso, decimal imponibile, decimal speseArt15)
            => AvvisoPdfServiceTests.Calcola(avviso, imponibile, speseArt15);

        public Task<IReadOnlyList<AvvisoFattura>> ElencoPerAttivitaAsync(Guid idAttivita, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<AttivitaFatturabile>> AttivitaConAvvisiNonFatturatiAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<ScadenzaFatturabile>> ScadenzeFatturabiliAsync(Guid idAttivita, Guid? idAvvisoEscluso = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<AttivitaFatturabile>> AttivitaFatturabiliAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<DettaglioDaSchedulare>> DettagliDaSchedulareAsync(Guid idAttivita, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Guid> EmettiAsync(EmissioneAvvisoRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AnnullaAsync(Guid idAvviso, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AggiornaTestataAsync(AvvisoFattura avviso, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AggiornaDettagliAsync(Guid idAvviso, IReadOnlyList<ModificaRigaAvvisoInput> righe, IReadOnlyList<Guid> idSpeseSelezionate, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeAnagraficaManager : IAnagraficaManager
    {
        private readonly Anagrafica _cliente;
        public FakeAnagraficaManager(Anagrafica cliente) => _cliente = cliente;

        public Task<Anagrafica?> GetByIdAsync(Guid idAnagrafica, CancellationToken cancellationToken = default)
            => Task.FromResult<Anagrafica?>(_cliente);

        public Task<IReadOnlyList<Anagrafica>> ElencoAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Guid> CreaAsync(Anagrafica anagrafica, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task AggiornaAsync(Anagrafica anagrafica, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task EliminaAsync(Guid idAnagrafica, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> EEliminabileAsync(Guid idAnagrafica, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class FakeAttivitaManager : IAttivitaManager
    {
        private readonly Attivita _attivita;
        public FakeAttivitaManager(Attivita attivita) => _attivita = attivita;

        public Task<Attivita?> GetByIdAsync(Guid idAttivita, CancellationToken cancellationToken = default)
            => Task.FromResult<Attivita?>(_attivita);

        public Task<IReadOnlyList<Attivita>> ElencoAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Attivita>> ElencoPerAnagraficaAsync(Guid idAnagrafica, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Attivita>> ElencoPerAnagraficaTipoAsync(Guid idAnagrafica, Guid idTipoAttivita, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Guid> CreaAsync(Attivita attivita, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task AggiornaAsync(Attivita attivita, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task EliminaAsync(Guid idAttivita, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> EEliminabileAsync(Guid idAttivita, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class FakeCodicePagamentoManager : ICodicePagamentoManager
    {
        public Task<CodicePagamento?> GetByIdAsync(Guid idCodicePagamento, CancellationToken cancellationToken = default)
            => Task.FromResult<CodicePagamento?>(new CodicePagamento
            {
                IdCodicePagamento = idCodicePagamento, IdTipoPagamento = Guid.NewGuid(),
                DescrPag = "A VISTA", NumScadenze = 1, GGScad1 = 0, FineMese = false,
            });

        public Task<IReadOnlyList<CodicePagamentoRiga>> ElencoAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Guid> CreaAsync(CodicePagamento codice, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task AggiornaAsync(CodicePagamento codice, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task EliminaAsync(Guid idCodicePagamento, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> EEliminabileAsync(Guid idCodicePagamento, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class FakeBancaAppoggioManager : IBancaAppoggioManager
    {
        public Task<BancaAppoggioRiga?> GetByIdAsync(Guid idBancaAppoggio, CancellationToken cancellationToken = default)
            => Task.FromResult<BancaAppoggioRiga?>(new BancaAppoggioRiga(
                idBancaAppoggio, null, Guid.NewGuid(),
                "Banco Popolare di Verona e Novara", "05034", Guid.NewGuid(),
                "Piazza Erbe \"B\"", "11750", "IT74H0503411750000000003403", true));

        public Task<IReadOnlyList<BancaAppoggioRiga>> ElencoAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<BancaAppoggioRiga>> SelezionabiliAsync(Guid? idCliente, bool bancheAzienda, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Guid> CreaAsync(BancaAppoggioInput input, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task AggiornaAsync(BancaAppoggioInput input, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task EliminaAsync(Guid idBancaAppoggio, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> EEliminabileAsync(Guid idBancaAppoggio, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class FakeSpesaAnticipataManager : ISpesaAnticipataManager
    {
        public Task<IReadOnlyList<SpesaAnticipata>> ElencoPerAvvisoAsync(Guid idAvviso, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SpesaAnticipata>>(Array.Empty<SpesaAnticipata>());

        public Task<IReadOnlyList<SpesaAnticipata>> ElencoPerAttivitaAsync(Guid idAttivita, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SpesaAnticipata>> ElencoFatturabiliPerAttivitaAsync(Guid idAttivita, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Guid> CreaAsync(SpesaAnticipata spesa, CancellationToken ct = default) => throw new NotImplementedException();
        public Task AggiornaAsync(SpesaAnticipata spesa, CancellationToken ct = default) => throw new NotImplementedException();
        public Task EliminaAsync(Guid idSpesaAnticipata, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeAziendaManager : IAziendaManager
    {
        private readonly Azienda? _azienda;
        public FakeAziendaManager(Azienda? azienda) => _azienda = azienda;

        public Task<Azienda?> GetAziendaAsync(CancellationToken ct = default) => Task.FromResult(_azienda);
    }
}
