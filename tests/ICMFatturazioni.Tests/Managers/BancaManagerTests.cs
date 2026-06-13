using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test di <see cref="BancaManager"/>: logica get-or-create per nome, aggiorno
/// l'ABI di una banca esistente (niente doppioni), audit dei soli eventi reali.
/// </summary>
public class BancaManagerTests
{
    private static BancaManager NewSut(FakeBancaRepository repo, FakeAuditManager? audit = null)
        => new(repo, audit ?? new FakeAuditManager());

    [Fact]
    public async Task RisolviAsync_BancaNuova_CreaERegistraAudit()
    {
        var repo = new FakeBancaRepository();
        var audit = new FakeAuditManager();
        var sut = NewSut(repo, audit);

        var id = await sut.RisolviAsync("Intesa Sanpaolo", "03069");

        Assert.True(repo.Store.ContainsKey(id));
        Assert.Equal("03069", repo.Store[id].ABI);
        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Creazione, voce.Operazione);
        Assert.Equal("Banca", voce.EntityType);
    }

    [Fact]
    public async Task RisolviAsync_BancaEsistenteStessoNome_RiusaSenzaDuplicare()
    {
        var repo = new FakeBancaRepository();
        var sut = NewSut(repo);

        var id1 = await sut.RisolviAsync("Unicredit", "02008");
        var id2 = await sut.RisolviAsync("  unicredit ", "02008"); // stesso nome (CI + trim)

        Assert.Equal(id1, id2);
        Assert.Single(repo.Store);
    }

    [Fact]
    public async Task RisolviAsync_AbiDiverso_AggiornaLaBancaEsistente()
    {
        var repo = new FakeBancaRepository();
        var audit = new FakeAuditManager();
        var sut = NewSut(repo, audit);
        var id = await sut.RisolviAsync("Unicredit", "02008");
        audit.Voci.Clear();

        var id2 = await sut.RisolviAsync("Unicredit", "99999");

        Assert.Equal(id, id2);
        Assert.Equal("99999", repo.Store[id].ABI);
        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Modifica, voce.Operazione);
    }

    [Fact]
    public async Task RisolviAsync_AbiVuoto_NonAzzeraQuelloEsistente()
    {
        var repo = new FakeBancaRepository();
        var sut = NewSut(repo);
        var id = await sut.RisolviAsync("Unicredit", "02008");

        // Seconda risoluzione senza ABI: non deve cancellare quello esistente.
        await sut.RisolviAsync("Unicredit", null);

        Assert.Equal("02008", repo.Store[id].ABI);
    }
}
