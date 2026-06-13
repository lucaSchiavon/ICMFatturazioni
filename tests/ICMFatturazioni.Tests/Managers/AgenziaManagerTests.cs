using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test di <see cref="AgenziaManager"/>: get-or-create per (banca, nome),
/// aggiorno il CAB di un'agenzia esistente (è il fix del problema "stessa
/// agenzia, CAB divergente"), audit dei soli eventi reali.
/// </summary>
public class AgenziaManagerTests
{
    private static AgenziaManager NewSut(FakeAgenziaRepository repo, FakeAuditManager? audit = null)
        => new(repo, audit ?? new FakeAuditManager());

    [Fact]
    public async Task RisolviAsync_NomeVuoto_RestituisceNull()
    {
        var sut = NewSut(new FakeAgenziaRepository());
        Assert.Null(await sut.RisolviAsync(Guid.CreateVersion7(), "  ", "11101"));
    }

    [Fact]
    public async Task RisolviAsync_AgenziaNuova_CreaERegistraAudit()
    {
        var repo = new FakeAgenziaRepository();
        var audit = new FakeAuditManager();
        var sut = NewSut(repo, audit);
        var idBanca = Guid.CreateVersion7();

        var id = await sut.RisolviAsync(idBanca, "Piazza Erbe", "11101");

        Assert.NotNull(id);
        Assert.Equal("11101", repo.Store[id!.Value].CAB);
        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Creazione, voce.Operazione);
        Assert.Equal("Agenzia", voce.EntityType);
    }

    [Fact]
    public async Task RisolviAsync_StessoNomeStessaBanca_RiusaSenzaDuplicare()
    {
        var repo = new FakeAgenziaRepository();
        var sut = NewSut(repo);
        var idBanca = Guid.CreateVersion7();

        var id1 = await sut.RisolviAsync(idBanca, "Piazza Erbe", "11101");
        var id2 = await sut.RisolviAsync(idBanca, " piazza erbe ", "11101");

        Assert.Equal(id1, id2);
        Assert.Single(repo.Store);
    }

    [Fact]
    public async Task RisolviAsync_CabDiverso_AggiornaLAgenziaEsistente_NonDuplica()
    {
        var repo = new FakeAgenziaRepository();
        var sut = NewSut(repo);
        var idBanca = Guid.CreateVersion7();
        var id = await sut.RisolviAsync(idBanca, "Piazza Erbe", "11101");

        var id2 = await sut.RisolviAsync(idBanca, "Piazza Erbe", "99999");

        Assert.Equal(id, id2);
        Assert.Single(repo.Store);
        Assert.Equal("99999", repo.Store[id!.Value].CAB);
    }

    [Fact]
    public async Task RisolviAsync_StessoNomeBancheDiverse_SonoAgenzieDistinte()
    {
        var repo = new FakeAgenziaRepository();
        var sut = NewSut(repo);
        var bancaA = Guid.CreateVersion7();
        var bancaB = Guid.CreateVersion7();

        var idA = await sut.RisolviAsync(bancaA, "Sede", "11111");
        var idB = await sut.RisolviAsync(bancaB, "Sede", "22222");

        Assert.NotEqual(idA, idB);
        Assert.Equal(2, repo.Store.Count);
    }
}
