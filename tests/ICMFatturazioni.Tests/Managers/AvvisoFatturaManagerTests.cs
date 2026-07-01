using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;
using ICMFatturazioni.Web.Models;
using ICMFatturazioni.Web.Services;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test del manager AvvisoFattura: emissione atomica (snapshot fiscali/pagamento,
/// righe con importi autorevoli, lock rate, link spese), annullamento, aggiornamento
/// testata, calcolo sugli snapshot, validazioni e audit.
/// </summary>
public class AvvisoFatturaManagerTests
{
    private static readonly Guid IdAttivita = Guid.NewGuid();

    private sealed class Sut
    {
        public required AvvisoFatturaManager           Manager  { get; init; }
        public required FakeAvvisoFatturaRepository     Repo     { get; init; }
        public required FakeScadenzaPagamentoRepository Scadenze { get; init; }
        public required FakeAnagraficaRepository        Anag     { get; init; }
        public required FakeAliquotaManager             Aliquote { get; init; }
        public required FakeAuditManager                Audit    { get; init; }
    }

    private static Sut NewSut()
    {
        var repo     = new FakeAvvisoFatturaRepository();
        var scadenze = new FakeScadenzaPagamentoRepository();
        var anag     = new FakeAnagraficaRepository();
        var aliquote = new FakeAliquotaManager();
        var audit    = new FakeAuditManager();
        var manager  = new AvvisoFatturaManager(
            repo, scadenze, anag, aliquote, new CalcoloFiscaleAvviso(), audit);
        return new Sut { Manager = manager, Repo = repo, Scadenze = scadenze, Anag = anag, Aliquote = aliquote, Audit = audit };
    }

    private static async Task<Guid> SeedAnagraficaAsync(
        FakeAnagraficaRepository anag,
        bool  sostituto = true,
        Guid? idPag     = null,
        Guid? idBanca   = null)
    {
        var id = Guid.NewGuid();
        await anag.InsertAsync(new Anagrafica
        {
            IdAnagrafica     = id,
            TipoAnagrafica   = TipoAnagrafica.Societa,
            RagioneSociale   = "ACME S.r.l.",
            IdPag            = idPag,
            IdBancaAppoggio  = idBanca,
            SostitutoImposta = sostituto,
        });
        return id;
    }

    private static ScadenzaFatturabile Fatturabile(
        Guid     idScadenza,
        decimal  importo             = 1000m,
        int      ordineDettaglio     = 1,
        string   descrizione         = "PRESTAZIONE",
        string?  tipo                = "DISCIPLINARE",
        Guid?    idDettaglio         = null) => new(
        IdScadenza:                  idScadenza,
        IdAttivitaDettaglio:         idDettaglio ?? Guid.NewGuid(),
        DataScadenza:                new DateOnly(2026, 9, 30),
        Importo:                     importo,
        Nota:                        null,
        OrdineDettaglio:             ordineDettaglio,
        IdTipoDettaglioAttivita:     Guid.NewGuid(),
        TipoDettaglioDescrizione:    tipo,
        DescrizioneDettaglio:        descrizione,
        ImportoDettaglio:            importo,
        GiaAllocatoAvvisiPrecedenti: 0m);

    private static EmissioneAvvisoRequest Request(
        Guid                 idAnagrafica,
        IReadOnlyList<Guid>  scadenze,
        IReadOnlyList<Guid>? spese = null,
        decimal              aliquotaIva = 22m,
        DateOnly?            data = null,
        Guid?                idCodicePagamento = null,
        Guid?                idBancaAppoggio = null,
        IReadOnlyList<string>? descrittive = null)
    {
        // Righe ordinate: prima le scadenze, poi le eventuali descrittive.
        var righe = scadenze.Select(s => new RigaAvvisoInput(s))
            .Concat((descrittive ?? Array.Empty<string>()).Select(d => new RigaAvvisoInput(null, d)))
            .ToList();
        return new(
            IdAttivita:               IdAttivita,
            IdAnagrafica:             idAnagrafica,
            DataAvviso:               data ?? new DateOnly(2026, 10, 1),
            AliquotaIva:              aliquotaIva,
            Righe:                    righe,
            IdSpeseSelezionate:       spese ?? Array.Empty<Guid>(),
            IdCodicePagamento:        idCodicePagamento,
            IdBancaAppoggio:          idBancaAppoggio);
    }

    // =====================================================================
    // Emissione — happy path
    // =====================================================================

    [Fact]
    public async Task EmettiAsync_Valido_CreaTestataConSnapshotECostruisceRighe()
    {
        var sut = NewSut();
        var idAnag = await SeedAnagraficaAsync(sut.Anag, sostituto: true, idPag: Guid.NewGuid(), idBanca: Guid.NewGuid());
        var s1 = Guid.NewGuid();
        var s2 = Guid.NewGuid();
        var dett = Guid.NewGuid();
        sut.Scadenze.Fatturabili.Add(Fatturabile(s1, importo: 1000m, idDettaglio: dett));
        sut.Scadenze.Fatturabili.Add(Fatturabile(s2, importo: 500m, idDettaglio: dett));

        var id = await sut.Manager.EmettiAsync(Request(idAnag, new[] { s1, s2 }, aliquotaIva: 22m));

        var dettaglio = await sut.Manager.GetDettaglioAsync(id);
        Assert.NotNull(dettaglio);
        var t = dettaglio!.Testata;
        Assert.Equal(22m, t.AliquotaIva);
        Assert.Equal(4m, t.AliquotaCnpaia);       // dallo FakeAliquotaManager
        Assert.Equal(20m, t.AliquotaRitenuta);
        Assert.True(t.ApplicaRitenuta);           // anagrafica sostituto d'imposta
        Assert.True(t.IsAttivo);

        Assert.Equal(2, dettaglio.Righe.Count);
        Assert.All(dettaglio.Righe, r => Assert.False(r.IsDescrittiva));
        Assert.Equal(new decimal?[] { 1000m, 500m }, dettaglio.Righe.Select(r => r.Importo));
        Assert.Equal("PRESTAZIONE", dettaglio.Righe[0].Descrizione);
        Assert.Equal("DISCIPLINARE", dettaglio.Righe[0].Tipo);
    }

    [Fact]
    public async Task EmettiAsync_UsaImportoAutorevoleDalDb_NonQuelloEventualmentePassato()
    {
        // La rata si fattura intera: l'importo della riga è quello del read-model.
        var sut = NewSut();
        var idAnag = await SeedAnagraficaAsync(sut.Anag);
        var s1 = Guid.NewGuid();
        sut.Scadenze.Fatturabili.Add(Fatturabile(s1, importo: 777.77m));

        var id = await sut.Manager.EmettiAsync(Request(idAnag, new[] { s1 }));

        var righe = await sut.Repo.GetRigheByAvvisoAsync(id);
        Assert.Equal(777.77m, Assert.Single(righe).Importo);
    }

    [Fact]
    public async Task EmettiAsync_ApplicaRitenutaSegueSostitutoImposta()
    {
        var sut = NewSut();
        var idAnag = await SeedAnagraficaAsync(sut.Anag, sostituto: false);
        var s1 = Guid.NewGuid();
        sut.Scadenze.Fatturabili.Add(Fatturabile(s1));

        var id = await sut.Manager.EmettiAsync(Request(idAnag, new[] { s1 }));

        Assert.False((await sut.Repo.GetByIdAsync(id))!.ApplicaRitenuta);
    }

    [Fact]
    public async Task EmettiAsync_EreditaPagamentoEBancaDallAnagrafica()
    {
        var sut = NewSut();
        var pag = Guid.NewGuid();
        var banca = Guid.NewGuid();
        var idAnag = await SeedAnagraficaAsync(sut.Anag, idPag: pag, idBanca: banca);
        var s1 = Guid.NewGuid();
        sut.Scadenze.Fatturabili.Add(Fatturabile(s1));

        var id = await sut.Manager.EmettiAsync(Request(idAnag, new[] { s1 }));

        var t = (await sut.Repo.GetByIdAsync(id))!;
        Assert.Equal(pag, t.IdCodicePagamento);
        Assert.Equal(banca, t.IdBancaAppoggio);
    }

    [Fact]
    public async Task EmettiAsync_OverridePagamentoEBanca_VinceSullAnagrafica()
    {
        var sut = NewSut();
        var idAnag = await SeedAnagraficaAsync(sut.Anag, idPag: Guid.NewGuid(), idBanca: Guid.NewGuid());
        var s1 = Guid.NewGuid();
        sut.Scadenze.Fatturabili.Add(Fatturabile(s1));
        var pagOverride = Guid.NewGuid();
        var bancaOverride = Guid.NewGuid();

        var id = await sut.Manager.EmettiAsync(
            Request(idAnag, new[] { s1 }, idCodicePagamento: pagOverride, idBancaAppoggio: bancaOverride));

        var t = (await sut.Repo.GetByIdAsync(id))!;
        Assert.Equal(pagOverride, t.IdCodicePagamento);
        Assert.Equal(bancaOverride, t.IdBancaAppoggio);
    }

    [Fact]
    public async Task EmettiAsync_RigheDescrittive_AccodateSenzaScadenzaNeImporto()
    {
        var sut = NewSut();
        var idAnag = await SeedAnagraficaAsync(sut.Anag);
        var s1 = Guid.NewGuid();
        sut.Scadenze.Fatturabili.Add(Fatturabile(s1));

        var id = await sut.Manager.EmettiAsync(Request(idAnag, new[] { s1 },
            descrittive: new[] { "Nota a piè di elenco" }));

        var righe = await sut.Repo.GetRigheByAvvisoAsync(id);
        Assert.Equal(2, righe.Count);
        var descr = righe.Single(r => r.IsDescrittiva);
        Assert.Null(descr.Importo);
        Assert.Null(descr.IdScadenza);
        Assert.Equal("Nota a piè di elenco", descr.Descrizione);
    }

    [Fact]
    public async Task EmettiAsync_PreservaOrdineRigheInterlacciate()
    {
        // Ordine bozza: scadenza s1, riga descrittiva, scadenza s2 → Ordine 1,2,3.
        var sut = NewSut();
        var idAnag = await SeedAnagraficaAsync(sut.Anag);
        var s1 = Guid.NewGuid();
        var s2 = Guid.NewGuid();
        sut.Scadenze.Fatturabili.Add(Fatturabile(s1, importo: 100m));
        sut.Scadenze.Fatturabili.Add(Fatturabile(s2, importo: 200m));

        var request = new EmissioneAvvisoRequest(
            IdAttivita:         IdAttivita,
            IdAnagrafica:       idAnag,
            DataAvviso:         new DateOnly(2026, 10, 1),
            AliquotaIva:        22m,
            Righe:              new[] { new RigaAvvisoInput(s1), new RigaAvvisoInput(null, "Nota in mezzo"), new RigaAvvisoInput(s2) },
            IdSpeseSelezionate: Array.Empty<Guid>());

        var id = await sut.Manager.EmettiAsync(request);

        var righe = (await sut.Repo.GetRigheByAvvisoAsync(id)).OrderBy(r => r.Ordine).ToList();
        Assert.Equal(3, righe.Count);
        Assert.False(righe[0].IsDescrittiva);
        Assert.True(righe[1].IsDescrittiva);
        Assert.Equal("Nota in mezzo", righe[1].Descrizione);
        Assert.False(righe[2].IsDescrittiva);
        Assert.Equal(new[] { 1, 2, 3 }, righe.Select(r => r.Ordine));
    }

    [Fact]
    public async Task EmettiAsync_CollegaSpeseSelezionate()
    {
        var sut = NewSut();
        var idAnag = await SeedAnagraficaAsync(sut.Anag);
        var s1 = Guid.NewGuid();
        sut.Scadenze.Fatturabili.Add(Fatturabile(s1));
        var spesa1 = Guid.NewGuid();
        var spesa2 = Guid.NewGuid();

        var id = await sut.Manager.EmettiAsync(Request(idAnag, new[] { s1 }, spese: new[] { spesa1, spesa2 }));

        Assert.Equal(new[] { spesa1, spesa2 }, sut.Repo.SpeseCollegate[id]);
    }

    [Fact]
    public async Task EmettiAsync_RegistraAuditCreazione()
    {
        var sut = NewSut();
        var idAnag = await SeedAnagraficaAsync(sut.Anag);
        var s1 = Guid.NewGuid();
        sut.Scadenze.Fatturabili.Add(Fatturabile(s1));

        var id = await sut.Manager.EmettiAsync(Request(idAnag, new[] { s1 }));

        var voce = Assert.Single(sut.Audit.Voci);
        Assert.Equal(AuditOperazione.Creazione, voce.Operazione);
        Assert.Equal("AvvisoFattura", voce.EntityType);
        Assert.Equal(id, voce.EntityId);
    }

    // =====================================================================
    // Emissione — validazioni
    // =====================================================================

    [Fact]
    public async Task EmettiAsync_SenzaScadenze_Lancia()
    {
        var sut = NewSut();
        var idAnag = await SeedAnagraficaAsync(sut.Anag);

        var ex = await Assert.ThrowsAsync<AvvisoFatturaInvalidaException>(
            () => sut.Manager.EmettiAsync(Request(idAnag, Array.Empty<Guid>())));
        Assert.Equal(AvvisoFatturaMotivoInvalido.NessunaScadenzaSelezionata, ex.Motivo);
    }

    [Fact]
    public async Task EmettiAsync_DataDefault_Lancia()
    {
        var sut = NewSut();
        var idAnag = await SeedAnagraficaAsync(sut.Anag);
        var s1 = Guid.NewGuid();
        sut.Scadenze.Fatturabili.Add(Fatturabile(s1));

        var ex = await Assert.ThrowsAsync<AvvisoFatturaInvalidaException>(
            () => sut.Manager.EmettiAsync(Request(idAnag, new[] { s1 }, data: default(DateOnly))));
        Assert.Equal(AvvisoFatturaMotivoInvalido.DataObbligatoria, ex.Motivo);
    }

    [Fact]
    public async Task EmettiAsync_AnagraficaInesistente_Lancia()
    {
        var sut = NewSut();
        var s1 = Guid.NewGuid();
        sut.Scadenze.Fatturabili.Add(Fatturabile(s1));

        var ex = await Assert.ThrowsAsync<AvvisoFatturaInvalidaException>(
            () => sut.Manager.EmettiAsync(Request(Guid.NewGuid(), new[] { s1 })));
        Assert.Equal(AvvisoFatturaMotivoInvalido.AnagraficaNonTrovata, ex.Motivo);
    }

    [Fact]
    public async Task EmettiAsync_ScadenzaNonFatturabile_Lancia()
    {
        var sut = NewSut();
        var idAnag = await SeedAnagraficaAsync(sut.Anag);
        // Nessuna fatturabile seminata: la scadenza scelta non è disponibile.

        var ex = await Assert.ThrowsAsync<AvvisoFatturaInvalidaException>(
            () => sut.Manager.EmettiAsync(Request(idAnag, new[] { Guid.NewGuid() })));
        Assert.Equal(AvvisoFatturaMotivoInvalido.ScadenzaNonFatturabile, ex.Motivo);
    }

    [Fact]
    public async Task EmettiAsync_RataGiaConsumata_PropagaScadenzaGiaInAvviso()
    {
        var sut = NewSut();
        var idAnag = await SeedAnagraficaAsync(sut.Anag);
        var s1 = Guid.NewGuid();
        sut.Scadenze.Fatturabili.Add(Fatturabile(s1));

        await sut.Manager.EmettiAsync(Request(idAnag, new[] { s1 }));

        // Secondo avviso sulla stessa rata → guardia indice univoco (dal repo).
        var ex = await Assert.ThrowsAsync<AvvisoFatturaInvalidaException>(
            () => sut.Manager.EmettiAsync(Request(idAnag, new[] { s1 })));
        Assert.Equal(AvvisoFatturaMotivoInvalido.ScadenzaGiaInAvviso, ex.Motivo);
    }

    // =====================================================================
    // Annullamento
    // =====================================================================

    [Fact]
    public async Task AnnullaAsync_SoftDeleteSbloccaRateEAudita()
    {
        var sut = NewSut();
        var idAnag = await SeedAnagraficaAsync(sut.Anag);
        var s1 = Guid.NewGuid();
        sut.Scadenze.Fatturabili.Add(Fatturabile(s1));
        var id = await sut.Manager.EmettiAsync(Request(idAnag, new[] { s1 }));
        sut.Audit.Voci.Clear();

        await sut.Manager.AnnullaAsync(id);

        Assert.False((await sut.Repo.GetByIdAsync(id))!.IsAttivo);
        Assert.DoesNotContain(s1, sut.Repo.ScadenzeConsumate.Keys); // rata di nuovo fatturabile
        var voce = Assert.Single(sut.Audit.Voci);
        Assert.Equal(AuditOperazione.Eliminazione, voce.Operazione);
    }

    [Fact]
    public async Task AnnullaAsync_PermetteRifatturareLaRata()
    {
        var sut = NewSut();
        var idAnag = await SeedAnagraficaAsync(sut.Anag);
        var s1 = Guid.NewGuid();
        sut.Scadenze.Fatturabili.Add(Fatturabile(s1));
        var id1 = await sut.Manager.EmettiAsync(Request(idAnag, new[] { s1 }));
        await sut.Manager.AnnullaAsync(id1);

        // Ora la rata è libera: un nuovo avviso non deve fallire.
        var id2 = await sut.Manager.EmettiAsync(Request(idAnag, new[] { s1 }));
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public async Task AnnullaAsync_Inesistente_NoOp()
    {
        var sut = NewSut();
        await sut.Manager.AnnullaAsync(Guid.NewGuid());
        Assert.Empty(sut.Audit.Voci);
    }

    [Fact]
    public async Task AnnullaAsync_GiaAnnullato_NoOp()
    {
        var sut = NewSut();
        var idAnag = await SeedAnagraficaAsync(sut.Anag);
        var s1 = Guid.NewGuid();
        sut.Scadenze.Fatturabili.Add(Fatturabile(s1));
        var id = await sut.Manager.EmettiAsync(Request(idAnag, new[] { s1 }));
        await sut.Manager.AnnullaAsync(id);
        sut.Audit.Voci.Clear();

        await sut.Manager.AnnullaAsync(id);
        Assert.Empty(sut.Audit.Voci);
    }

    // =====================================================================
    // Aggiornamento testata
    // =====================================================================

    [Fact]
    public async Task AggiornaTestataAsync_PersisteEAudita()
    {
        var sut = NewSut();
        var idAnag = await SeedAnagraficaAsync(sut.Anag);
        var s1 = Guid.NewGuid();
        sut.Scadenze.Fatturabili.Add(Fatturabile(s1));
        var id = await sut.Manager.EmettiAsync(Request(idAnag, new[] { s1 }, aliquotaIva: 22m));
        sut.Audit.Voci.Clear();

        var testata = (await sut.Repo.GetByIdAsync(id))!;
        var modificata = new AvvisoFattura
        {
            IdAvviso     = testata.IdAvviso,
            IdAttivita   = testata.IdAttivita,
            IdAnagrafica = testata.IdAnagrafica,
            DataAvviso   = testata.DataAvviso,
            Oggetto      = "Oggetto aggiornato",
            AliquotaIva  = testata.AliquotaIva,
            AliquotaCnpaia = testata.AliquotaCnpaia,
            AliquotaRitenuta = testata.AliquotaRitenuta,
            ApplicaRitenuta = testata.ApplicaRitenuta,
        };
        await sut.Manager.AggiornaTestataAsync(modificata);

        Assert.Equal("Oggetto aggiornato", (await sut.Repo.GetByIdAsync(id))!.Oggetto);
        var voce = Assert.Single(sut.Audit.Voci);
        Assert.Equal(AuditOperazione.Modifica, voce.Operazione);
    }

    [Fact]
    public async Task AggiornaTestataAsync_DataDefault_Lancia()
    {
        var sut = NewSut();
        var avviso = new AvvisoFattura
        {
            IdAvviso     = Guid.NewGuid(),
            IdAttivita   = IdAttivita,
            IdAnagrafica = Guid.NewGuid(),
            DataAvviso   = default,
        };

        var ex = await Assert.ThrowsAsync<AvvisoFatturaInvalidaException>(
            () => sut.Manager.AggiornaTestataAsync(avviso));
        Assert.Equal(AvvisoFatturaMotivoInvalido.DataObbligatoria, ex.Motivo);
    }

    // =====================================================================
    // Calcolo (sugli snapshot dell'avviso)
    // =====================================================================

    [Fact]
    public void Calcola_UsaGliSnapshotDellAvviso()
    {
        var sut = NewSut();
        var avviso = new AvvisoFattura
        {
            IdAvviso         = Guid.NewGuid(),
            IdAttivita       = IdAttivita,
            IdAnagrafica     = Guid.NewGuid(),
            DataAvviso       = new DateOnly(2026, 10, 1),
            AliquotaIva      = 22m,
            AliquotaCnpaia   = 4m,
            AliquotaRitenuta = 20m,
            ApplicaRitenuta  = true,
        };

        var r = sut.Manager.Calcola(avviso, imponibile: 1000m, speseArt15: 0m);

        // 1000 + 4% cassa = 1040; IVA 22% = 228.80; totale 1268.80; ritenuta 200; avere 1068.80
        Assert.Equal(40m, r.Cassa);
        Assert.Equal(228.80m, r.Iva);
        Assert.Equal(1268.80m, r.Totale);
        Assert.Equal(200m, r.Ritenuta);
        Assert.Equal(1068.80m, r.TotaleNostroAvere);
    }

    [Fact]
    public void Calcola_SenzaRitenuta_QuandoNonSostituto()
    {
        var sut = NewSut();
        var avviso = new AvvisoFattura
        {
            IdAvviso         = Guid.NewGuid(),
            IdAttivita       = IdAttivita,
            IdAnagrafica     = Guid.NewGuid(),
            DataAvviso       = new DateOnly(2026, 10, 1),
            AliquotaIva      = 22m,
            AliquotaCnpaia   = 4m,
            AliquotaRitenuta = 20m,
            ApplicaRitenuta  = false,
        };

        var r = sut.Manager.Calcola(avviso, imponibile: 1000m, speseArt15: 0m);

        Assert.Equal(0m, r.Ritenuta);
        Assert.Equal(1268.80m, r.TotaleNostroAvere);
    }

    // =====================================================================
    // Letture
    // =====================================================================

    [Fact]
    public async Task ScadenzeFatturabiliAsync_Delega()
    {
        var sut = NewSut();
        sut.Scadenze.Fatturabili.Add(Fatturabile(Guid.NewGuid()));
        sut.Scadenze.Fatturabili.Add(Fatturabile(Guid.NewGuid()));

        var lista = await sut.Manager.ScadenzeFatturabiliAsync(IdAttivita);

        Assert.Equal(2, lista.Count);
    }

    [Fact]
    public async Task GetDettaglioAsync_Inesistente_RestituisceNull()
    {
        var sut = NewSut();
        Assert.Null(await sut.Manager.GetDettaglioAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ElencoPerAttivitaAsync_SoloAttiviDellAttivita()
    {
        var sut = NewSut();
        var idAnag = await SeedAnagraficaAsync(sut.Anag);
        var s1 = Guid.NewGuid();
        var s2 = Guid.NewGuid();
        sut.Scadenze.Fatturabili.Add(Fatturabile(s1));
        sut.Scadenze.Fatturabili.Add(Fatturabile(s2));
        var id1 = await sut.Manager.EmettiAsync(Request(idAnag, new[] { s1 }));
        var id2 = await sut.Manager.EmettiAsync(Request(idAnag, new[] { s2 }));
        await sut.Manager.AnnullaAsync(id2);

        var lista = await sut.Manager.ElencoPerAttivitaAsync(IdAttivita);

        Assert.Single(lista);
        Assert.Equal(id1, lista[0].IdAvviso);
    }
}
