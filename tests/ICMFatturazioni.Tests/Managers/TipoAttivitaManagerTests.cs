using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test del manager Tipi attività: normalizzazione (trim, maiuscolo), validazione,
/// unicità, audit, doppia difesa su delete.
/// </summary>
public class TipoAttivitaManagerTests
{
    private static TipoAttivita Tipo(string descr = "CONSULENZE", GestisciCome gestisci = GestisciCome.Consulenza, bool studi = true) => new()
    {
        Descrizione  = descr,
        GestisciCome = gestisci,
        StudiSettore = studi,
    };

    private static TipoAttivitaManager NewSut(FakeTipoAttivitaRepository fake, FakeAuditManager? audit = null)
        => new(fake, audit ?? new FakeAuditManager());

    [Fact]
    public async Task CreaAsync_Valido_RestituisceIdEPersiste()
    {
        var fake = new FakeTipoAttivitaRepository();
        var sut = NewSut(fake);

        var id = await sut.CreaAsync(Tipo());

        Assert.NotEqual(Guid.Empty, id);
        var t = await fake.GetByIdAsync(id);
        Assert.Equal("CONSULENZE", t!.Descrizione);
        Assert.Equal(GestisciCome.Consulenza, t.GestisciCome);
        Assert.True(t.StudiSettore);
    }

    [Fact]
    public async Task CreaAsync_RegistraAuditDiCreazione()
    {
        var fake = new FakeTipoAttivitaRepository();
        var audit = new FakeAuditManager();
        var sut = NewSut(fake, audit);

        var id = await sut.CreaAsync(Tipo());

        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Creazione, voce.Operazione);
        Assert.Equal("TipoAttivita", voce.EntityType);
        Assert.Equal(id, voce.EntityId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreaAsync_DescrizioneVuota_LanciaObbligatoria(string descr)
    {
        var sut = NewSut(new FakeTipoAttivitaRepository());
        var ex = await Assert.ThrowsAsync<TipoAttivitaInvalidaException>(() => sut.CreaAsync(Tipo(descr: descr)));
        Assert.Equal(TipoAttivitaInvalidoMotivo.DescrizioneObbligatoria, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_DescrizioneDuplicata_Lancia()
    {
        var fake = new FakeTipoAttivitaRepository();
        var sut = NewSut(fake);
        await sut.CreaAsync(Tipo(descr: "CONSULENZE"));

        var ex = await Assert.ThrowsAsync<TipoAttivitaInvalidaException>(
            () => sut.CreaAsync(Tipo(descr: "consulenze")));  // case-insensitive
        Assert.Equal(TipoAttivitaInvalidoMotivo.DescrizioneDuplicata, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_NormalizzaTrimEMaiuscolo()
    {
        var fake = new FakeTipoAttivitaRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Tipo(descr: "  Consulenze  "));
        var t = await fake.GetByIdAsync(id);
        Assert.Equal("CONSULENZE", t!.Descrizione);
    }

    [Fact]
    public async Task CreaAsync_GestisciComeProgetto_Persiste()
    {
        var fake = new FakeTipoAttivitaRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Tipo(descr: "PROGETTAZIONI", gestisci: GestisciCome.Progetto, studi: false));
        var t = await fake.GetByIdAsync(id);
        Assert.Equal(GestisciCome.Progetto, t!.GestisciCome);
        Assert.False(t.StudiSettore);
    }

    [Fact]
    public async Task AggiornaAsync_StessoTipo_NonConsideraDuplicato()
    {
        var fake = new FakeTipoAttivitaRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Tipo(descr: "CONSULENZE", gestisci: GestisciCome.Consulenza));

        await sut.AggiornaAsync(new TipoAttivita
        {
            IdTipoAttivita = id,
            Descrizione    = "CONSULENZE",
            GestisciCome   = GestisciCome.Progetto,
            StudiSettore   = false,
        });
        var t = await fake.GetByIdAsync(id);
        Assert.Equal(GestisciCome.Progetto, t!.GestisciCome);
    }

    [Fact]
    public async Task EliminaAsync_ConDipendenze_Lancia_NonDisattiva()
    {
        var fake = new FakeTipoAttivitaRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Tipo());
        fake.DipendenzeDa.Add(id);

        await Assert.ThrowsAsync<TipoAttivitaConDipendenzeException>(() => sut.EliminaAsync(id));
        Assert.True((await fake.GetByIdAsync(id))!.IsAttivo);
    }

    [Fact]
    public async Task EliminaAsync_SenzaDipendenze_DisattivaEAudita()
    {
        var fake = new FakeTipoAttivitaRepository();
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
        var fake = new FakeTipoAttivitaRepository();
        var sut = NewSut(fake);
        await sut.CreaAsync(Tipo(descr: "PROGETTAZIONI", gestisci: GestisciCome.Progetto));
        await sut.CreaAsync(Tipo(descr: "CONSULENZE"));

        var elenco = await sut.ElencoAsync();
        Assert.Equal("CONSULENZE", elenco[0].Descrizione);
        Assert.Equal("PROGETTAZIONI", elenco[1].Descrizione);
    }

    [Fact]
    public async Task EEliminabileAsync_SenzaDipendenze_True()
    {
        var fake = new FakeTipoAttivitaRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Tipo());
        Assert.True(await sut.EEliminabileAsync(id));
    }

    [Fact]
    public async Task EEliminabileAsync_ConDipendenze_False()
    {
        var fake = new FakeTipoAttivitaRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Tipo());
        fake.DipendenzeDa.Add(id);
        Assert.False(await sut.EEliminabileAsync(id));
    }
}
