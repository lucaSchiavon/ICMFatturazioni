using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test del manager Tipi di pagamento: validazione/unicità (descrizione+sigla),
/// normalizzazione (trim, sigla maiuscola), audit, doppia difesa su delete.
/// </summary>
public class TipoPagamentoManagerTests
{
    private static TipoPagamento Tipo(string descr = "Bonifico", string? sigla = "BO", FlagBanca flag = FlagBanca.Azienda) => new()
    {
        Descrizione = descr,
        SiglaPag = sigla,
        FlagBanca = flag,
    };

    private static TipoPagamentoManager NewSut(FakeTipoPagamentoRepository fake, FakeAuditManager? audit = null)
        => new(fake, audit ?? new FakeAuditManager());

    [Fact]
    public async Task CreaAsync_Valido_RestituisceIdEPersiste()
    {
        var fake = new FakeTipoPagamentoRepository();
        var sut = NewSut(fake);

        var id = await sut.CreaAsync(Tipo());

        Assert.NotEqual(Guid.Empty, id);
        var p = await fake.GetByIdAsync(id);
        Assert.Equal("Bonifico", p!.Descrizione);
        Assert.Equal(FlagBanca.Azienda, p.FlagBanca);
    }

    [Fact]
    public async Task CreaAsync_RegistraAuditDiCreazione()
    {
        var fake = new FakeTipoPagamentoRepository();
        var audit = new FakeAuditManager();
        var sut = NewSut(fake, audit);

        var id = await sut.CreaAsync(Tipo());

        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Creazione, voce.Operazione);
        Assert.Equal("TipoPagamento", voce.EntityType);
        Assert.Equal(id, voce.EntityId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreaAsync_DescrizioneVuota_LanciaObbligatoria(string descr)
    {
        var sut = NewSut(new FakeTipoPagamentoRepository());
        var ex = await Assert.ThrowsAsync<TipoPagamentoInvalidaException>(() => sut.CreaAsync(Tipo(descr: descr)));
        Assert.Equal(TipoPagamentoInvalidoMotivo.DescrizioneObbligatoria, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_DescrizioneDuplicata_Lancia()
    {
        var fake = new FakeTipoPagamentoRepository();
        var sut = NewSut(fake);
        await sut.CreaAsync(Tipo(descr: "Bonifico", sigla: "BO"));

        var ex = await Assert.ThrowsAsync<TipoPagamentoInvalidaException>(
            () => sut.CreaAsync(Tipo(descr: "bonifico", sigla: "B2")));  // case-insensitive
        Assert.Equal(TipoPagamentoInvalidoMotivo.DescrizioneDuplicata, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_SiglaDuplicata_Lancia()
    {
        var fake = new FakeTipoPagamentoRepository();
        var sut = NewSut(fake);
        await sut.CreaAsync(Tipo(descr: "Bonifico", sigla: "BO"));

        var ex = await Assert.ThrowsAsync<TipoPagamentoInvalidaException>(
            () => sut.CreaAsync(Tipo(descr: "Bonifico estero", sigla: "bo")));  // case-insensitive
        Assert.Equal(TipoPagamentoInvalidoMotivo.SiglaDuplicata, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_SenzaSigla_Ammesso()
    {
        var fake = new FakeTipoPagamentoRepository();
        var sut = NewSut(fake);
        await sut.CreaAsync(Tipo(descr: "Contanti", sigla: null));
        // Una seconda senza sigla non collide (sigla nulla = nessun vincolo).
        var id = await sut.CreaAsync(Tipo(descr: "Assegno", sigla: "   "));
        var p = await fake.GetByIdAsync(id);
        Assert.Null(p!.SiglaPag);
    }

    [Fact]
    public async Task CreaAsync_NormalizzaTrimESiglaMaiuscola()
    {
        var fake = new FakeTipoPagamentoRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(new TipoPagamento { Descrizione = "  Bonifico  ", SiglaPag = " bo ", FlagBanca = FlagBanca.Azienda });
        var p = await fake.GetByIdAsync(id);
        Assert.Equal("Bonifico", p!.Descrizione);
        Assert.Equal("BO", p.SiglaPag);
    }

    [Fact]
    public async Task AggiornaAsync_StessoTipo_NonConsideraDuplicato()
    {
        var fake = new FakeTipoPagamentoRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Tipo(descr: "Bonifico", sigla: "BO"));

        await sut.AggiornaAsync(new TipoPagamento
        {
            IdTipoPagamento = id, Descrizione = "Bonifico", SiglaPag = "BO", FlagBanca = FlagBanca.Cliente,
        });
        var p = await fake.GetByIdAsync(id);
        Assert.Equal(FlagBanca.Cliente, p!.FlagBanca);
    }

    [Fact]
    public async Task EliminaAsync_ConDipendenze_Lancia_NonDisattiva()
    {
        var fake = new FakeTipoPagamentoRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Tipo());
        fake.DipendenzeDa.Add(id);

        await Assert.ThrowsAsync<TipoPagamentoConDipendenzeException>(() => sut.EliminaAsync(id));
        Assert.True((await fake.GetByIdAsync(id))!.IsAttivo);
    }

    [Fact]
    public async Task EliminaAsync_SenzaDipendenze_DisattivaEAudita()
    {
        var fake = new FakeTipoPagamentoRepository();
        var audit = new FakeAuditManager();
        var sut = NewSut(fake, audit);
        var id = await sut.CreaAsync(Tipo());
        audit.Voci.Clear();

        await sut.EliminaAsync(id);

        Assert.False((await fake.GetByIdAsync(id))!.IsAttivo);
        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Eliminazione, voce.Operazione);
    }

    [Fact]
    public async Task ElencoAsync_SoloAttiviOrdinatiPerDescrizione()
    {
        var fake = new FakeTipoPagamentoRepository();
        var sut = NewSut(fake);
        await sut.CreaAsync(Tipo(descr: "Ricevute bancarie", sigla: "RB", flag: FlagBanca.Cliente));
        await sut.CreaAsync(Tipo(descr: "Bonifico", sigla: "BO"));

        var elenco = await sut.ElencoAsync();
        Assert.Equal("Bonifico", elenco[0].Descrizione);
        Assert.Equal("Ricevute bancarie", elenco[1].Descrizione);
    }
}
