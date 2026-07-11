using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test del manager Pagamenti consulenti: validazione, guardia D-C3 (tranche mai
/// oltre il residuo, anche in modifica; residuo mai negativo), pagamenti solo su
/// righe a carico Studio, saldo per tranche successive (esempio dispensa cap. 5),
/// audit su tutto il CRUD.
/// </summary>
public class AttivitaConsulentePagamentoManagerTests
{
    private static readonly Guid IdAttivita = Guid.NewGuid();

    private static AttivitaConsulentePagamento Tranche(Guid idRiga, decimal importo, DateOnly? data = null, string? nota = null) => new()
    {
        IdAttivitaConsulente = idRiga,
        DataPagamento        = data ?? new DateOnly(2026, 7, 9),
        Importo              = importo,
        Nota                 = nota,
    };

    private static AttivitaConsulentePagamentoManager NewSut(FakeAttivitaConsulentePagamentoRepository fake, FakeAuditManager? audit = null)
        => new(fake, audit ?? new FakeAuditManager());

    // ─── Creazione ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreaAsync_Valida_PersisteEAudita()
    {
        var fake = new FakeAttivitaConsulentePagamentoRepository();
        var audit = new FakeAuditManager();
        var riga = fake.AddRiga(IdAttivita, importo: 5000m);
        var sut = NewSut(fake, audit);

        var id = await sut.CreaAsync(Tranche(riga, 2000m));

        Assert.NotEqual(Guid.Empty, id);
        var p = await fake.GetByIdAsync(id);
        Assert.Equal(2000m, p!.Importo);
        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Creazione, voce.Operazione);
        Assert.Equal("AttivitaConsulentePagamento", voce.EntityType);
    }

    [Fact]
    public async Task CreaAsync_RigaVuota_Lancia()
    {
        var sut = NewSut(new FakeAttivitaConsulentePagamentoRepository());
        var ex = await Assert.ThrowsAsync<AttivitaConsulentePagamentoInvalidoException>(
            () => sut.CreaAsync(Tranche(Guid.Empty, 100m)));
        Assert.Equal(AttivitaConsulentePagamentoInvalidoMotivo.RigaObbligatoria, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_RigaInesistente_Lancia()
    {
        var sut = NewSut(new FakeAttivitaConsulentePagamentoRepository());
        var ex = await Assert.ThrowsAsync<AttivitaConsulentePagamentoInvalidoException>(
            () => sut.CreaAsync(Tranche(Guid.NewGuid(), 100m)));
        Assert.Equal(AttivitaConsulentePagamentoInvalidoMotivo.RigaNonTrovata, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_RigaACaricoCliente_Lancia()
    {
        var fake = new FakeAttivitaConsulentePagamentoRepository();
        var riga = fake.AddRiga(IdAttivita, importo: 5000m, caricoStudio: false);
        var sut = NewSut(fake);

        var ex = await Assert.ThrowsAsync<AttivitaConsulentePagamentoInvalidoException>(
            () => sut.CreaAsync(Tranche(riga, 100m)));
        Assert.Equal(AttivitaConsulentePagamentoInvalidoMotivo.RigaNonACaricoStudio, ex.Motivo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public async Task CreaAsync_ImportoNonPositivo_Lancia(decimal importo)
    {
        var fake = new FakeAttivitaConsulentePagamentoRepository();
        var riga = fake.AddRiga(IdAttivita, importo: 5000m);
        var sut = NewSut(fake);

        var ex = await Assert.ThrowsAsync<AttivitaConsulentePagamentoInvalidoException>(
            () => sut.CreaAsync(Tranche(riga, importo)));
        Assert.Equal(AttivitaConsulentePagamentoInvalidoMotivo.ImportoNonPositivo, ex.Motivo);
    }

    // ─── Guardia D-C3 ────────────────────────────────────────────────────

    [Fact]
    public async Task CreaAsync_TrancheOltreResiduo_Lancia()
    {
        var fake = new FakeAttivitaConsulentePagamentoRepository();
        var riga = fake.AddRiga(IdAttivita, importo: 5000m);
        var sut = NewSut(fake);
        await sut.CreaAsync(Tranche(riga, 2000m));   // residuo 3000

        var ex = await Assert.ThrowsAsync<AttivitaConsulentePagamentoInvalidoException>(
            () => sut.CreaAsync(Tranche(riga, 3500m)));   // > 3000
        Assert.Equal(AttivitaConsulentePagamentoInvalidoMotivo.ImportoOltreResiduo, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_TranchePariAlResiduo_SaldaLaConsulenza()
    {
        // Esempio dispensa cap. 5: 5000 → −2000 → 3000 → −1000 → 2000 → −2000 → 0.
        var fake = new FakeAttivitaConsulentePagamentoRepository();
        var riga = fake.AddRiga(IdAttivita, importo: 5000m);
        var sut = NewSut(fake);

        await sut.CreaAsync(Tranche(riga, 2000m));
        await sut.CreaAsync(Tranche(riga, 1000m));
        await sut.CreaAsync(Tranche(riga, 2000m));   // esattamente il residuo: ok

        var saldi = await sut.ConsulenzeConSaldoAsync(IdAttivita);
        var s = Assert.Single(saldi);
        Assert.Equal(5000m, s.Pagato);
        Assert.Equal(0m, s.Residuo);
    }

    [Fact]
    public async Task AggiornaAsync_EntroIlResiduo_EscludeSeStessa()
    {
        // Riga 5000 con tranche 2000+2000 (pagato 4000, residuo 1000): portare la
        // seconda tranche a 3000 è LECITO (senza di lei il residuo è 3000).
        var fake = new FakeAttivitaConsulentePagamentoRepository();
        var riga = fake.AddRiga(IdAttivita, importo: 5000m);
        var sut = NewSut(fake);
        await sut.CreaAsync(Tranche(riga, 2000m));
        var id2 = await sut.CreaAsync(Tranche(riga, 2000m));

        var mod = Tranche(riga, 3000m);
        mod.IdConsulentePagamento = id2;
        await sut.AggiornaAsync(mod);

        Assert.Equal(3000m, (await fake.GetByIdAsync(id2))!.Importo);
        var s = Assert.Single(await sut.ConsulenzeConSaldoAsync(IdAttivita));
        Assert.Equal(0m, s.Residuo);
    }

    [Fact]
    public async Task AggiornaAsync_OltreIlResiduo_Lancia()
    {
        // Riga 5000 con tranche 2000+2000: la seconda non può salire a 3500
        // (2000 + 3500 = 5500 > 5000 → residuo negativo, vietato da D-C3).
        var fake = new FakeAttivitaConsulentePagamentoRepository();
        var riga = fake.AddRiga(IdAttivita, importo: 5000m);
        var sut = NewSut(fake);
        await sut.CreaAsync(Tranche(riga, 2000m));
        var id2 = await sut.CreaAsync(Tranche(riga, 2000m));

        var mod = Tranche(riga, 3500m);
        mod.IdConsulentePagamento = id2;
        var ex = await Assert.ThrowsAsync<AttivitaConsulentePagamentoInvalidoException>(
            () => sut.AggiornaAsync(mod));
        Assert.Equal(AttivitaConsulentePagamentoInvalidoMotivo.ImportoOltreResiduo, ex.Motivo);
        Assert.Equal(2000m, (await fake.GetByIdAsync(id2))!.Importo);   // invariata
    }

    // ─── Eliminazione ────────────────────────────────────────────────────

    [Fact]
    public async Task EliminaAsync_DisattivaEIlResiduoRisale()
    {
        var fake = new FakeAttivitaConsulentePagamentoRepository();
        var audit = new FakeAuditManager();
        var riga = fake.AddRiga(IdAttivita, importo: 5000m);
        var sut = NewSut(fake, audit);
        var id = await sut.CreaAsync(Tranche(riga, 2000m));
        audit.Voci.Clear();

        await sut.EliminaAsync(id);

        Assert.False((await fake.GetByIdAsync(id))!.IsAttivo);
        var s = Assert.Single(await sut.ConsulenzeConSaldoAsync(IdAttivita));
        Assert.Equal(0m, s.Pagato);
        Assert.Equal(5000m, s.Residuo);
        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Eliminazione, voce.Operazione);
    }

    // ─── Read-model ──────────────────────────────────────────────────────

    [Fact]
    public async Task ConsulenzeConSaldoAsync_SoloRigheCaricoStudio()
    {
        var fake = new FakeAttivitaConsulentePagamentoRepository();
        var rigaStudio  = fake.AddRiga(IdAttivita, importo: 5000m, caricoStudio: true);
        fake.AddRiga(IdAttivita, importo: 1000m, caricoStudio: false);   // esclusa
        fake.AddRiga(Guid.NewGuid(), importo: 900m);                     // altra attività
        var sut = NewSut(fake);

        var saldi = await sut.ConsulenzeConSaldoAsync(IdAttivita);

        var s = Assert.Single(saldi);
        Assert.Equal(rigaStudio, s.IdAttivitaConsulente);
    }

    [Fact]
    public async Task ElencoPerRigaAsync_SoloTrancheAttiveOrdinatePerData()
    {
        var fake = new FakeAttivitaConsulentePagamentoRepository();
        var riga = fake.AddRiga(IdAttivita, importo: 5000m);
        var sut = NewSut(fake);
        await sut.CreaAsync(Tranche(riga, 1000m, new DateOnly(2026, 7, 20)));
        var idEliminata = await sut.CreaAsync(Tranche(riga, 500m, new DateOnly(2026, 7, 1)));
        await sut.CreaAsync(Tranche(riga, 800m, new DateOnly(2026, 7, 10)));
        await sut.EliminaAsync(idEliminata);

        var elenco = await sut.ElencoPerRigaAsync(riga);

        Assert.Equal(2, elenco.Count);
        Assert.Equal(new DateOnly(2026, 7, 10), elenco[0].DataPagamento);
        Assert.Equal(new DateOnly(2026, 7, 20), elenco[1].DataPagamento);
    }

    [Fact]
    public async Task CreaAsync_NotaVuota_NormalizzataANull()
    {
        var fake = new FakeAttivitaConsulentePagamentoRepository();
        var riga = fake.AddRiga(IdAttivita, importo: 5000m);
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Tranche(riga, 100m, nota: "   "));
        Assert.Null((await fake.GetByIdAsync(id))!.Nota);
    }
}
