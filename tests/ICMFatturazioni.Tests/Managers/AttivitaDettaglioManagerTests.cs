using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test del manager AttivitaDettaglio: validazione campi, ordine automatico,
/// protezione HasFattura, riordinamento e audit.
/// </summary>
public class AttivitaDettaglioManagerTests
{
    private static readonly Guid IdAttivita       = Guid.NewGuid();
    private static readonly Guid IdTipoDettaglio  = Guid.NewGuid();

    private static AttivitaDettaglio Det(
        string   descr      = "Disciplinare",
        decimal  importo    = 1000m,
        Guid?    idAttivita = null,
        Guid?    idTipo     = null) => new()
    {
        IdAttivita              = idAttivita ?? IdAttivita,
        IdTipoDettaglioAttivita = idTipo     ?? IdTipoDettaglio,
        DescrizioneDettaglio    = descr,
        Importo                 = importo,
    };

    private static AttivitaDettaglioManager NewSut(
        FakeAttivitaDettaglioRepository fake,
        FakeAuditManager? audit = null)
        => new(fake, audit ?? new FakeAuditManager());

    // -------------------------------------------------------------------------
    // Creazione e ordine automatico
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreaAsync_Valido_RestituisceIdEAssegnaOrdine()
    {
        var fake = new FakeAttivitaDettaglioRepository();
        var sut  = NewSut(fake);

        var id = await sut.CreaAsync(Det());

        Assert.NotEqual(Guid.Empty, id);
        var d = await fake.GetByIdAsync(id);
        Assert.NotNull(d);
        Assert.Equal(1, d.Ordine);
        Assert.Equal("Disciplinare", d.DescrizioneDettaglio);
    }

    [Fact]
    public async Task CreaAsync_PiuDettagli_OrdineProgressivo()
    {
        var fake = new FakeAttivitaDettaglioRepository();
        var sut  = NewSut(fake);

        var id1 = await sut.CreaAsync(Det(descr: "Prima"));
        var id2 = await sut.CreaAsync(Det(descr: "Seconda"));
        var id3 = await sut.CreaAsync(Det(descr: "Terza"));

        Assert.Equal(1, (await fake.GetByIdAsync(id1))!.Ordine);
        Assert.Equal(2, (await fake.GetByIdAsync(id2))!.Ordine);
        Assert.Equal(3, (await fake.GetByIdAsync(id3))!.Ordine);
    }

    [Fact]
    public async Task CreaAsync_RegistraAuditCreazione()
    {
        var fake  = new FakeAttivitaDettaglioRepository();
        var audit = new FakeAuditManager();
        var sut   = NewSut(fake, audit);

        var id = await sut.CreaAsync(Det());

        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Creazione, voce.Operazione);
        Assert.Equal("AttivitaDettaglio", voce.EntityType);
        Assert.Equal(id, voce.EntityId);
    }

    // -------------------------------------------------------------------------
    // Validazione campi obbligatori
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreaAsync_TipoVuoto_LanciaTipoObbligatorio()
    {
        var sut = NewSut(new FakeAttivitaDettaglioRepository());
        var ex  = await Assert.ThrowsAsync<AttivitaDettaglioInvalidaException>(
            () => sut.CreaAsync(Det(idTipo: Guid.Empty)));
        Assert.Equal(AttivitaDettaglioMotivoInvalido.TipoDettaglioObbligatorio, ex.Motivo);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreaAsync_DescrizioneVuota_LanciaDescrizioneObbligatoria(string descr)
    {
        var sut = NewSut(new FakeAttivitaDettaglioRepository());
        var ex  = await Assert.ThrowsAsync<AttivitaDettaglioInvalidaException>(
            () => sut.CreaAsync(Det(descr: descr)));
        Assert.Equal(AttivitaDettaglioMotivoInvalido.DescrizioneObbligatoria, ex.Motivo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreaAsync_ImportoNonPositivo_LanciaImportoNonValido(decimal importo)
    {
        var sut = NewSut(new FakeAttivitaDettaglioRepository());
        var ex  = await Assert.ThrowsAsync<AttivitaDettaglioInvalidaException>(
            () => sut.CreaAsync(Det(importo: importo)));
        Assert.Equal(AttivitaDettaglioMotivoInvalido.ImportoNonValido, ex.Motivo);
    }

    // -------------------------------------------------------------------------
    // Protezione HasFattura
    // -------------------------------------------------------------------------

    [Fact]
    public async Task AggiornaAsync_HasFattura_LanciaHasFattura()
    {
        var fake = new FakeAttivitaDettaglioRepository();
        var sut  = NewSut(fake);
        var id   = await sut.CreaAsync(Det());
        fake.SetHasFattura(id, true);

        var da = await fake.GetByIdAsync(id);
        var ex = await Assert.ThrowsAsync<AttivitaDettaglioInvalidaException>(
            () => sut.AggiornaAsync(da!));
        Assert.Equal(AttivitaDettaglioMotivoInvalido.HasFattura, ex.Motivo);
    }

    [Fact]
    public async Task EliminaAsync_HasFattura_LanciaHasFattura()
    {
        var fake = new FakeAttivitaDettaglioRepository();
        var sut  = NewSut(fake);
        var id   = await sut.CreaAsync(Det());
        fake.SetHasFattura(id, true);

        var ex = await Assert.ThrowsAsync<AttivitaDettaglioInvalidaException>(
            () => sut.EliminaAsync(id));
        Assert.Equal(AttivitaDettaglioMotivoInvalido.HasFattura, ex.Motivo);
        Assert.True((await fake.GetByIdAsync(id))!.IsAttivo);
    }

    [Fact]
    public async Task EliminaAsync_SenzaFattura_DisattivaEAudita()
    {
        var fake  = new FakeAttivitaDettaglioRepository();
        var audit = new FakeAuditManager();
        var sut   = NewSut(fake, audit);
        var id    = await sut.CreaAsync(Det());
        audit.Voci.Clear();

        await sut.EliminaAsync(id);

        Assert.False((await fake.GetByIdAsync(id))!.IsAttivo);
        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Eliminazione, voce.Operazione);
    }

    // -------------------------------------------------------------------------
    // Riordinamento
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SpostaSuAsync_SecondaRiga_DiventaPrima()
    {
        var fake = new FakeAttivitaDettaglioRepository();
        var sut  = NewSut(fake);
        var id1  = await sut.CreaAsync(Det(descr: "Prima"));
        var id2  = await sut.CreaAsync(Det(descr: "Seconda"));

        await sut.SpostaSuAsync(id2);

        var lista = await sut.ElencoPerAttivitaAsync(IdAttivita);
        Assert.Equal("Seconda", lista[0].DescrizioneDettaglio);
        Assert.Equal("Prima",   lista[1].DescrizioneDettaglio);
    }

    [Fact]
    public async Task SpostaSuAsync_GiaPrima_Noop()
    {
        var fake = new FakeAttivitaDettaglioRepository();
        var sut  = NewSut(fake);
        var id1  = await sut.CreaAsync(Det(descr: "Prima"));
        var id2  = await sut.CreaAsync(Det(descr: "Seconda"));

        await sut.SpostaSuAsync(id1); // no-op

        var lista = await sut.ElencoPerAttivitaAsync(IdAttivita);
        Assert.Equal("Prima",   lista[0].DescrizioneDettaglio);
        Assert.Equal("Seconda", lista[1].DescrizioneDettaglio);
    }

    [Fact]
    public async Task SpostaGiuAsync_PrimaRiga_DiventaSeconda()
    {
        var fake = new FakeAttivitaDettaglioRepository();
        var sut  = NewSut(fake);
        var id1  = await sut.CreaAsync(Det(descr: "Prima"));
        var id2  = await sut.CreaAsync(Det(descr: "Seconda"));

        await sut.SpostaGiuAsync(id1);

        var lista = await sut.ElencoPerAttivitaAsync(IdAttivita);
        Assert.Equal("Seconda", lista[0].DescrizioneDettaglio);
        Assert.Equal("Prima",   lista[1].DescrizioneDettaglio);
    }

    [Fact]
    public async Task SpostaGiuAsync_GiaUltima_Noop()
    {
        var fake = new FakeAttivitaDettaglioRepository();
        var sut  = NewSut(fake);
        var id1  = await sut.CreaAsync(Det(descr: "Prima"));
        var id2  = await sut.CreaAsync(Det(descr: "Seconda"));

        await sut.SpostaGiuAsync(id2); // no-op

        var lista = await sut.ElencoPerAttivitaAsync(IdAttivita);
        Assert.Equal("Prima",   lista[0].DescrizioneDettaglio);
        Assert.Equal("Seconda", lista[1].DescrizioneDettaglio);
    }

    // -------------------------------------------------------------------------
    // Elenco
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ElencoPerAttivitaAsync_OrdinaPerOrdineAsc()
    {
        var fake = new FakeAttivitaDettaglioRepository();
        var sut  = NewSut(fake);
        await sut.CreaAsync(Det(descr: "A"));
        await sut.CreaAsync(Det(descr: "B"));
        await sut.CreaAsync(Det(descr: "C"));

        var lista = await sut.ElencoPerAttivitaAsync(IdAttivita);

        Assert.Equal(3, lista.Count);
        Assert.Equal("A", lista[0].DescrizioneDettaglio);
        Assert.Equal("B", lista[1].DescrizioneDettaglio);
        Assert.Equal("C", lista[2].DescrizioneDettaglio);
    }
}
