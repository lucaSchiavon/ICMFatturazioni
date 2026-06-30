using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test del manager Aliquota: CRUD, validazione, protezione aliquote di sistema,
/// lettura tipizzata CNPAIA/ritenuta per il calcolo dell'avviso.
/// </summary>
public class AliquotaManagerTests
{
    private static Aliquota Libera(string descrizione = "Sconto", decimal valore = 10m) => new()
    {
        Descrizione = descrizione,
        Valore      = valore,
    };

    private static (AliquotaManager sut, FakeAliquotaRepository repo, FakeAuditManager audit) NewSut(
        FakeAliquotaRepository? repo = null)
    {
        repo ??= new FakeAliquotaRepository();
        var audit = new FakeAuditManager();
        return (new AliquotaManager(repo, audit), repo, audit);
    }

    // ── Creazione / validazione ─────────────────────────────────────────────

    [Fact]
    public async Task CreaAsync_Valida_RestituisceIdEAudita()
    {
        var (sut, repo, audit) = NewSut();

        var id = await sut.CreaAsync(Libera("Maggiorazione X", 7.5m));

        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(7.5m, (await repo.GetByIdAsync(id))!.Valore);
        Assert.Equal(AuditOperazione.Creazione, Assert.Single(audit.Voci).Operazione);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreaAsync_DescrizioneVuota_Lancia(string descr)
    {
        var (sut, _, _) = NewSut();
        var ex = await Assert.ThrowsAsync<AliquotaInvalidaException>(() => sut.CreaAsync(Libera(descr)));
        Assert.Equal(AliquotaMotivoInvalido.DescrizioneObbligatoria, ex.Motivo);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task CreaAsync_ValoreFuoriIntervallo_Lancia(decimal valore)
    {
        var (sut, _, _) = NewSut();
        var ex = await Assert.ThrowsAsync<AliquotaInvalidaException>(() => sut.CreaAsync(Libera(valore: valore)));
        Assert.Equal(AliquotaMotivoInvalido.ValoreNonValido, ex.Motivo);
    }

    // ── Eliminazione / protezione sistema ───────────────────────────────────

    [Fact]
    public async Task EliminaAsync_AliquotaLibera_Disattiva()
    {
        var (sut, repo, _) = NewSut();
        var id = await sut.CreaAsync(Libera());

        await sut.EliminaAsync(id);

        Assert.False((await repo.GetByIdAsync(id))!.IsAttivo);
    }

    [Fact]
    public async Task EliminaAsync_AliquotaDiSistema_Lancia()
    {
        var repo = new FakeAliquotaRepository().Con("CNPAIA", "Cassa", 4m);
        var (sut, _, _) = NewSut(repo);
        var sistema = (await sut.ElencoAsync()).Single();

        var ex = await Assert.ThrowsAsync<AliquotaInvalidaException>(() => sut.EliminaAsync(sistema.IdAliquota));
        Assert.Equal(AliquotaMotivoInvalido.AliquotaDiSistema, ex.Motivo);
        Assert.True((await sut.GetByIdAsync(sistema.IdAliquota))!.IsAttivo);
    }

    // ── Lettura aliquote per il calcolo avviso ───────────────────────────────

    [Fact]
    public async Task GetAliquoteAvvisoAsync_LeggeCnpaiaERitenuta()
    {
        var repo = new FakeAliquotaRepository()
            .Con("CNPAIA", "Cassa", 4m)
            .Con("RITENUTA", "Ritenuta", 20m);
        var (sut, _, _) = NewSut(repo);

        var a = await sut.GetAliquoteAvvisoAsync();

        Assert.Equal(4m, a.Cnpaia);
        Assert.Equal(20m, a.Ritenuta);
    }

    [Fact]
    public async Task GetAliquoteAvvisoAsync_CodiceMancante_UsaDefault()
    {
        var (sut, _, _) = NewSut();   // tabella vuota

        var a = await sut.GetAliquoteAvvisoAsync();

        Assert.Equal(4m, a.Cnpaia);
        Assert.Equal(20m, a.Ritenuta);
    }

    [Fact]
    public async Task GetAliquoteAvvisoAsync_ValorePersonalizzato_Rispettato()
    {
        var repo = new FakeAliquotaRepository()
            .Con("CNPAIA", "Cassa", 5m)
            .Con("RITENUTA", "Ritenuta", 23m);
        var (sut, _, _) = NewSut(repo);

        var a = await sut.GetAliquoteAvvisoAsync();

        Assert.Equal(5m, a.Cnpaia);
        Assert.Equal(23m, a.Ritenuta);
    }
}
