using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test del manager Tipi attività consulenti: normalizzazione (trim, maiuscolo),
/// validazione, unicità, audit, doppia difesa su delete.
/// </summary>
public class TipoAttivitaConsulenteManagerTests
{
    private static TipoAttivitaConsulente Tipo(string descr = "CALCOLI STRUTTURALI") => new()
    {
        Descrizione = descr,
    };

    private static TipoAttivitaConsulenteManager NewSut(FakeTipoAttivitaConsulenteRepository fake, FakeAuditManager? audit = null)
        => new(fake, audit ?? new FakeAuditManager());

    [Fact]
    public async Task CreaAsync_Valido_RestituisceIdEPersiste()
    {
        var fake = new FakeTipoAttivitaConsulenteRepository();
        var sut = NewSut(fake);

        var id = await sut.CreaAsync(Tipo());

        Assert.NotEqual(Guid.Empty, id);
        var t = await fake.GetByIdAsync(id);
        Assert.Equal("CALCOLI STRUTTURALI", t!.Descrizione);
        Assert.True(t.IsAttivo);
    }

    [Fact]
    public async Task CreaAsync_RegistraAuditDiCreazione()
    {
        var fake = new FakeTipoAttivitaConsulenteRepository();
        var audit = new FakeAuditManager();
        var sut = NewSut(fake, audit);

        var id = await sut.CreaAsync(Tipo());

        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Creazione, voce.Operazione);
        Assert.Equal("TipoAttivitaConsulente", voce.EntityType);
        Assert.Equal(id, voce.EntityId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreaAsync_DescrizioneVuota_LanciaObbligatoria(string descr)
    {
        var sut = NewSut(new FakeTipoAttivitaConsulenteRepository());
        var ex = await Assert.ThrowsAsync<TipoAttivitaConsulenteInvalidoException>(() => sut.CreaAsync(Tipo(descr)));
        Assert.Equal(TipoAttivitaConsulenteInvalidoMotivo.DescrizioneObbligatoria, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_DescrizioneDuplicata_Lancia()
    {
        var fake = new FakeTipoAttivitaConsulenteRepository();
        var sut = NewSut(fake);
        await sut.CreaAsync(Tipo("CALCOLI STRUTTURALI"));

        var ex = await Assert.ThrowsAsync<TipoAttivitaConsulenteInvalidoException>(
            () => sut.CreaAsync(Tipo("calcoli strutturali")));  // case-insensitive
        Assert.Equal(TipoAttivitaConsulenteInvalidoMotivo.DescrizioneDuplicata, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_NormalizzaTrimEMaiuscolo()
    {
        var fake = new FakeTipoAttivitaConsulenteRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Tipo("  Calcoli strutturali  "));
        var t = await fake.GetByIdAsync(id);
        Assert.Equal("CALCOLI STRUTTURALI", t!.Descrizione);
    }

    [Fact]
    public async Task AggiornaAsync_StessoTipo_NonConsideraDuplicato()
    {
        var fake = new FakeTipoAttivitaConsulenteRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Tipo("CALCOLI STRUTTURALI"));

        await sut.AggiornaAsync(new TipoAttivitaConsulente
        {
            IdTipoAttivitaConsulente = id,
            Descrizione              = "CALCOLI STRUTTURALI",
        });
        var t = await fake.GetByIdAsync(id);
        Assert.Equal("CALCOLI STRUTTURALI", t!.Descrizione);
    }

    [Fact]
    public async Task EliminaAsync_ConDipendenze_Lancia_NonDisattiva()
    {
        var fake = new FakeTipoAttivitaConsulenteRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Tipo());
        fake.DipendenzeDa.Add(id);

        await Assert.ThrowsAsync<TipoAttivitaConsulenteConDipendenzeException>(() => sut.EliminaAsync(id));
        Assert.True((await fake.GetByIdAsync(id))!.IsAttivo);
    }

    [Fact]
    public async Task EliminaAsync_SenzaDipendenze_DisattivaEAudita()
    {
        var fake = new FakeTipoAttivitaConsulenteRepository();
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
        var fake = new FakeTipoAttivitaConsulenteRepository();
        var sut = NewSut(fake);
        await sut.CreaAsync(Tipo("VERIFICHE SISMICHE"));
        await sut.CreaAsync(Tipo("CALCOLI STRUTTURALI"));

        var elenco = await sut.ElencoAsync();
        Assert.Equal("CALCOLI STRUTTURALI", elenco[0].Descrizione);
        Assert.Equal("VERIFICHE SISMICHE", elenco[1].Descrizione);
    }

    [Fact]
    public async Task EEliminabileAsync_SenzaDipendenze_True()
    {
        var fake = new FakeTipoAttivitaConsulenteRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Tipo());
        Assert.True(await sut.EEliminabileAsync(id));
    }

    [Fact]
    public async Task EEliminabileAsync_ConDipendenze_False()
    {
        var fake = new FakeTipoAttivitaConsulenteRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Tipo());
        fake.DipendenzeDa.Add(id);
        Assert.False(await sut.EEliminabileAsync(id));
    }
}
