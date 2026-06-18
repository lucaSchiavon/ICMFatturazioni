using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test del manager Tipi dettaglio attività: normalizzazione, validazione,
/// unicità, audit, doppia difesa su delete.
/// </summary>
public class TipoDettaglioAttivitaManagerTests
{
    private static TipoDettaglioAttivita Tipo(string descr = "DISCIPLINARE") => new()
    {
        Descrizione = descr,
    };

    private static TipoDettaglioAttivitaManager NewSut(FakeTipoDettaglioAttivitaRepository fake, FakeAuditManager? audit = null)
        => new(fake, audit ?? new FakeAuditManager());

    [Fact]
    public async Task CreaAsync_Valido_RestituisceIdEPersiste()
    {
        var fake = new FakeTipoDettaglioAttivitaRepository();
        var sut = NewSut(fake);

        var id = await sut.CreaAsync(Tipo());

        Assert.NotEqual(Guid.Empty, id);
        var t = await fake.GetByIdAsync(id);
        Assert.Equal("DISCIPLINARE", t!.Descrizione);
        Assert.True(t.IsAttivo);
    }

    [Fact]
    public async Task CreaAsync_RegistraAuditDiCreazione()
    {
        var fake = new FakeTipoDettaglioAttivitaRepository();
        var audit = new FakeAuditManager();
        var sut = NewSut(fake, audit);

        var id = await sut.CreaAsync(Tipo());

        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Creazione, voce.Operazione);
        Assert.Equal("TipoDettaglioAttivita", voce.EntityType);
        Assert.Equal(id, voce.EntityId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreaAsync_DescrizioneVuota_LanciaObbligatoria(string descr)
    {
        var sut = NewSut(new FakeTipoDettaglioAttivitaRepository());
        var ex = await Assert.ThrowsAsync<TipoDettaglioAttivitaInvalidaException>(() => sut.CreaAsync(Tipo(descr: descr)));
        Assert.Equal(TipoDettaglioAttivitaInvalidoMotivo.DescrizioneObbligatoria, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_DescrizioneDuplicata_Lancia()
    {
        var fake = new FakeTipoDettaglioAttivitaRepository();
        var sut = NewSut(fake);
        await sut.CreaAsync(Tipo(descr: "DISCIPLINARE"));

        var ex = await Assert.ThrowsAsync<TipoDettaglioAttivitaInvalidaException>(
            () => sut.CreaAsync(Tipo(descr: "disciplinare")));  // case-insensitive
        Assert.Equal(TipoDettaglioAttivitaInvalidoMotivo.DescrizioneDuplicata, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_NormalizzaTrimEMaiuscolo()
    {
        var fake = new FakeTipoDettaglioAttivitaRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Tipo(descr: "  Extra disciplinare  "));
        var t = await fake.GetByIdAsync(id);
        Assert.Equal("EXTRA DISCIPLINARE", t!.Descrizione);
    }

    [Fact]
    public async Task AggiornaAsync_StessoTipo_NonConsideraDuplicato()
    {
        var fake = new FakeTipoDettaglioAttivitaRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Tipo(descr: "DISCIPLINARE"));

        // Rinomina stesso elemento — non deve sollevare duplicato.
        await sut.AggiornaAsync(new TipoDettaglioAttivita
        {
            IdTipoDettaglioAttivita = id,
            Descrizione             = "DISCIPLINARE",
        });
        var t = await fake.GetByIdAsync(id);
        Assert.Equal("DISCIPLINARE", t!.Descrizione);
    }

    [Fact]
    public async Task EliminaAsync_ConDipendenze_Lancia_NonDisattiva()
    {
        var fake = new FakeTipoDettaglioAttivitaRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Tipo());
        fake.DipendenzeDa.Add(id);

        await Assert.ThrowsAsync<TipoDettaglioAttivitaConDipendenzeException>(() => sut.EliminaAsync(id));
        Assert.True((await fake.GetByIdAsync(id))!.IsAttivo);
    }

    [Fact]
    public async Task EliminaAsync_SenzaDipendenze_DisattivaEAudita()
    {
        var fake = new FakeTipoDettaglioAttivitaRepository();
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
        var fake = new FakeTipoDettaglioAttivitaRepository();
        var sut = NewSut(fake);
        await sut.CreaAsync(Tipo(descr: "VENDITA CESPITE"));
        await sut.CreaAsync(Tipo(descr: "DISCIPLINARE"));

        var elenco = await sut.ElencoAsync();
        Assert.Equal("DISCIPLINARE", elenco[0].Descrizione);
        Assert.Equal("VENDITA CESPITE", elenco[1].Descrizione);
    }

    [Fact]
    public async Task EEliminabileAsync_SenzaDipendenze_True()
    {
        var fake = new FakeTipoDettaglioAttivitaRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Tipo());
        Assert.True(await sut.EEliminabileAsync(id));
    }

    [Fact]
    public async Task EEliminabileAsync_ConDipendenze_False()
    {
        var fake = new FakeTipoDettaglioAttivitaRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Tipo());
        fake.DipendenzeDa.Add(id);
        Assert.False(await sut.EEliminabileAsync(id));
    }
}
