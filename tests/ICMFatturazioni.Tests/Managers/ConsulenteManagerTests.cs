using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test del manager Consulenti: normalizzazione (trim, NO maiuscolo — il nome
/// resta com'è digitato), validazione, unicità, audit, doppia difesa su delete.
/// </summary>
public class ConsulenteManagerTests
{
    private static Consulente Consulente(string descr = "Luca Schiavon") => new()
    {
        Descrizione = descr,
    };

    private static ConsulenteManager NewSut(FakeConsulenteRepository fake, FakeAuditManager? audit = null)
        => new(fake, audit ?? new FakeAuditManager());

    [Fact]
    public async Task CreaAsync_Valido_RestituisceIdEPersiste()
    {
        var fake = new FakeConsulenteRepository();
        var sut = NewSut(fake);

        var id = await sut.CreaAsync(Consulente());

        Assert.NotEqual(Guid.Empty, id);
        var c = await fake.GetByIdAsync(id);
        Assert.Equal("Luca Schiavon", c!.Descrizione);
        Assert.True(c.IsAttivo);
    }

    [Fact]
    public async Task CreaAsync_RegistraAuditDiCreazione()
    {
        var fake = new FakeConsulenteRepository();
        var audit = new FakeAuditManager();
        var sut = NewSut(fake, audit);

        var id = await sut.CreaAsync(Consulente());

        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Creazione, voce.Operazione);
        Assert.Equal("Consulente", voce.EntityType);
        Assert.Equal(id, voce.EntityId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreaAsync_DescrizioneVuota_LanciaObbligatoria(string descr)
    {
        var sut = NewSut(new FakeConsulenteRepository());
        var ex = await Assert.ThrowsAsync<ConsulenteInvalidoException>(() => sut.CreaAsync(Consulente(descr)));
        Assert.Equal(ConsulenteInvalidoMotivo.DescrizioneObbligatoria, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_DescrizioneDuplicata_Lancia()
    {
        var fake = new FakeConsulenteRepository();
        var sut = NewSut(fake);
        await sut.CreaAsync(Consulente("Luca Schiavon"));

        var ex = await Assert.ThrowsAsync<ConsulenteInvalidoException>(
            () => sut.CreaAsync(Consulente("LUCA SCHIAVON")));  // case-insensitive
        Assert.Equal(ConsulenteInvalidoMotivo.DescrizioneDuplicata, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_NormalizzaTrim_MaNonMaiuscolo()
    {
        var fake = new FakeConsulenteRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Consulente("  Luca Schiavon  "));
        var c = await fake.GetByIdAsync(id);
        // Il nome del consulente NON viene forzato in maiuscolo (diversamente dai cataloghi).
        Assert.Equal("Luca Schiavon", c!.Descrizione);
    }

    [Fact]
    public async Task AggiornaAsync_StessoConsulente_NonConsideraDuplicato()
    {
        var fake = new FakeConsulenteRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Consulente("Luca Schiavon"));

        await sut.AggiornaAsync(new Consulente
        {
            IdConsulente = id,
            Descrizione  = "Luca Schiavon",
        });
        var c = await fake.GetByIdAsync(id);
        Assert.Equal("Luca Schiavon", c!.Descrizione);
    }

    [Fact]
    public async Task AggiornaAsync_RegistraAuditDiModifica()
    {
        var fake = new FakeConsulenteRepository();
        var audit = new FakeAuditManager();
        var sut = NewSut(fake, audit);
        var id = await sut.CreaAsync(Consulente("Luca Schiavon"));
        audit.Voci.Clear();

        await sut.AggiornaAsync(new Consulente { IdConsulente = id, Descrizione = "Mario Rossi" });

        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Modifica, voce.Operazione);
        Assert.Equal(id, voce.EntityId);
    }

    [Fact]
    public async Task EliminaAsync_ConDipendenze_Lancia_NonDisattiva()
    {
        var fake = new FakeConsulenteRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Consulente());
        fake.DipendenzeDa.Add(id);

        await Assert.ThrowsAsync<ConsulenteConDipendenzeException>(() => sut.EliminaAsync(id));
        Assert.True((await fake.GetByIdAsync(id))!.IsAttivo);
    }

    [Fact]
    public async Task EliminaAsync_SenzaDipendenze_DisattivaEAudita()
    {
        var fake = new FakeConsulenteRepository();
        var audit = new FakeAuditManager();
        var sut = NewSut(fake, audit);
        var id = await sut.CreaAsync(Consulente());
        audit.Voci.Clear();

        await sut.EliminaAsync(id);

        Assert.False((await fake.GetByIdAsync(id))!.IsAttivo);
        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Eliminazione, voce.Operazione);
    }

    [Fact]
    public async Task ElencoAsync_SoloAttiviOrdinatiPerNome()
    {
        var fake = new FakeConsulenteRepository();
        var sut = NewSut(fake);
        await sut.CreaAsync(Consulente("Mario Rossi"));
        await sut.CreaAsync(Consulente("Luca Schiavon"));

        var elenco = await sut.ElencoAsync();
        Assert.Equal("Luca Schiavon", elenco[0].Descrizione);
        Assert.Equal("Mario Rossi", elenco[1].Descrizione);
    }

    [Fact]
    public async Task EEliminabileAsync_SenzaDipendenze_True()
    {
        var fake = new FakeConsulenteRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Consulente());
        Assert.True(await sut.EEliminabileAsync(id));
    }

    [Fact]
    public async Task EEliminabileAsync_ConDipendenze_False()
    {
        var fake = new FakeConsulenteRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Consulente());
        fake.DipendenzeDa.Add(id);
        Assert.False(await sut.EEliminabileAsync(id));
    }
}
