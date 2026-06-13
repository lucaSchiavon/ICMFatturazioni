using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test del manager Codici di pagamento: validazioni cap. 4 (descrizione/tipo
/// obbligatori; NumScadenze 1..3; coerenza giorni; giorni aggiuntivi solo con
/// fine mese; descrizione univoca), normalizzazione, audit, doppia difesa delete.
/// </summary>
public class CodicePagamentoManagerTests
{
    private static readonly Guid TipoBonifico = Guid.Parse("7a000000-0000-0000-0000-000000000001");

    private static CodicePagamento Codice(
        string descr = "BONIFICO 60 GG F.M.", int num = 1, int gg1 = 60,
        int? gg2 = null, int? gg3 = null, int? ggPiu = null, bool fineMese = true, Guid? tipo = null) => new()
    {
        IdTipoPagamento = tipo ?? TipoBonifico,
        DescrPag = descr,
        NumScadenze = num,
        GGScad1 = gg1,
        GGScad2 = gg2,
        GGScad3 = gg3,
        GGpiu = ggPiu,
        FineMese = fineMese,
    };

    private static CodicePagamentoManager NewSut(FakeCodicePagamentoRepository fake, FakeAuditManager? audit = null)
        => new(fake, audit ?? new FakeAuditManager());

    [Fact]
    public async Task CreaAsync_Valido_PersisteEAudita()
    {
        var fake = new FakeCodicePagamentoRepository();
        var audit = new FakeAuditManager();
        var sut = NewSut(fake, audit);

        var id = await sut.CreaAsync(Codice());

        Assert.NotEqual(Guid.Empty, id);
        Assert.NotNull(await fake.GetByIdAsync(id));
        Assert.Contains(audit.Voci, v => v.EntityType == "CodicePagamento" && v.Operazione == AuditOperazione.Creazione);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreaAsync_DescrizioneVuota_Lancia(string descr)
    {
        var sut = NewSut(new FakeCodicePagamentoRepository());
        var ex = await Assert.ThrowsAsync<CodicePagamentoInvalidaException>(() => sut.CreaAsync(Codice(descr: descr)));
        Assert.Equal(CodicePagamentoInvalidoMotivo.DescrizioneObbligatoria, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_TipoMancante_Lancia()
    {
        var sut = NewSut(new FakeCodicePagamentoRepository());
        var ex = await Assert.ThrowsAsync<CodicePagamentoInvalidaException>(() => sut.CreaAsync(Codice(tipo: Guid.Empty)));
        Assert.Equal(CodicePagamentoInvalidoMotivo.TipoObbligatorio, ex.Motivo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public async Task CreaAsync_NumScadenzeFuoriRange_Lancia(int num)
    {
        var sut = NewSut(new FakeCodicePagamentoRepository());
        var ex = await Assert.ThrowsAsync<CodicePagamentoInvalidaException>(() => sut.CreaAsync(Codice(num: num, gg2: 60, gg3: 90)));
        Assert.Equal(CodicePagamentoInvalidoMotivo.NumScadenzeNonValido, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_DueScadenzeSenzaGG2_LanciaIncoerente()
    {
        var sut = NewSut(new FakeCodicePagamentoRepository());
        var ex = await Assert.ThrowsAsync<CodicePagamentoInvalidaException>(
            () => sut.CreaAsync(Codice(num: 2, gg1: 30, gg2: null)));
        Assert.Equal(CodicePagamentoInvalidoMotivo.GiorniScadenzaIncoerenti, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_GiorniAggiuntiviSenzaFineMese_Lancia()
    {
        var sut = NewSut(new FakeCodicePagamentoRepository());
        var ex = await Assert.ThrowsAsync<CodicePagamentoInvalidaException>(
            () => sut.CreaAsync(Codice(ggPiu: 10, fineMese: false)));
        Assert.Equal(CodicePagamentoInvalidoMotivo.GiorniAggiuntiviSenzaFineMese, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_NumScadenze1_AzzeraGG2eGG3()
    {
        var fake = new FakeCodicePagamentoRepository();
        var sut = NewSut(fake);
        // num=1 ma GG2/GG3 valorizzati per errore: la normalizzazione li azzera.
        var id = await sut.CreaAsync(Codice(num: 1, gg1: 0, gg2: 60, gg3: 90, fineMese: false));
        var p = await fake.GetByIdAsync(id);
        Assert.Null(p!.GGScad2);
        Assert.Null(p.GGScad3);
    }

    [Fact]
    public async Task CreaAsync_GGpiuZero_DiventaNull()
    {
        var fake = new FakeCodicePagamentoRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Codice(ggPiu: 0, fineMese: true));
        var p = await fake.GetByIdAsync(id);
        Assert.Null(p!.GGpiu);
    }

    [Fact]
    public async Task CreaAsync_DescrizioneDuplicata_Lancia()
    {
        var fake = new FakeCodicePagamentoRepository();
        var sut = NewSut(fake);
        await sut.CreaAsync(Codice(descr: "A VISTA", num: 1, gg1: 0, fineMese: false));

        var ex = await Assert.ThrowsAsync<CodicePagamentoInvalidaException>(
            () => sut.CreaAsync(Codice(descr: "a vista", num: 1, gg1: 0, fineMese: false)));
        Assert.Equal(CodicePagamentoInvalidoMotivo.DescrizioneDuplicata, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_TreScadenze_Persiste()
    {
        var fake = new FakeCodicePagamentoRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Codice(descr: "RIBA 30/60/90", num: 3, gg1: 30, gg2: 60, gg3: 90, fineMese: true));
        var p = await fake.GetByIdAsync(id);
        Assert.Equal(3, p!.NumScadenze);
        Assert.Equal(90, p.GGScad3);
    }

    [Fact]
    public async Task EliminaAsync_ConDipendenze_Lancia_NonDisattiva()
    {
        var fake = new FakeCodicePagamentoRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Codice());
        fake.DipendenzeDa.Add(id);

        await Assert.ThrowsAsync<CodicePagamentoConDipendenzeException>(() => sut.EliminaAsync(id));
        Assert.True((await fake.GetByIdAsync(id))!.IsAttivo);
    }

    [Fact]
    public async Task EliminaAsync_SenzaDipendenze_DisattivaEAudita()
    {
        var fake = new FakeCodicePagamentoRepository();
        var audit = new FakeAuditManager();
        var sut = NewSut(fake, audit);
        var id = await sut.CreaAsync(Codice());
        audit.Voci.Clear();

        await sut.EliminaAsync(id);

        Assert.False((await fake.GetByIdAsync(id))!.IsAttivo);
        Assert.Contains(audit.Voci, v => v.Operazione == AuditOperazione.Eliminazione);
    }
}
