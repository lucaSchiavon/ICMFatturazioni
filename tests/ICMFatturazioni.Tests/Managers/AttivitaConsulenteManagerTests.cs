using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test del manager Attività consulenti: validazione FK/importo, normalizzazione
/// nota, guardie sui pagamenti (D-C2 eliminazione bloccata, D-C3 residuo mai
/// negativo in modifica), audit su tutto il CRUD.
/// </summary>
public class AttivitaConsulenteManagerTests
{
    private static readonly Guid IdAttivita   = Guid.NewGuid();
    private static readonly Guid IdConsulente = Guid.NewGuid();
    private static readonly Guid IdTipo       = Guid.NewGuid();

    private static AttivitaConsulente Riga(
        decimal importo = 4000m,
        CaricoConsulenza carico = CaricoConsulenza.Studio,
        string? nota = null,
        Guid? idAttivita = null,
        Guid? idConsulente = null,
        Guid? idTipo = null) => new()
    {
        IdAttivita               = idAttivita ?? IdAttivita,
        IdConsulente             = idConsulente ?? IdConsulente,
        IdTipoAttivitaConsulente = idTipo ?? IdTipo,
        Carico                   = carico,
        Importo                  = importo,
        Nota                     = nota,
        ConsulenteDescrizione    = "Luca Schiavon",
    };

    private static AttivitaConsulenteManager NewSut(FakeAttivitaConsulenteRepository fake, FakeAuditManager? audit = null)
        => new(fake, audit ?? new FakeAuditManager());

    // ─── Creazione ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreaAsync_Valida_RestituisceIdEPersiste()
    {
        var fake = new FakeAttivitaConsulenteRepository();
        var sut = NewSut(fake);

        var id = await sut.CreaAsync(Riga());

        Assert.NotEqual(Guid.Empty, id);
        var r = await fake.GetByIdAsync(id);
        Assert.Equal(4000m, r!.Importo);
        Assert.Equal(CaricoConsulenza.Studio, r.Carico);
        Assert.True(r.IsAttivo);
    }

    [Fact]
    public async Task CreaAsync_RegistraAuditDiCreazione()
    {
        var fake = new FakeAttivitaConsulenteRepository();
        var audit = new FakeAuditManager();
        var sut = NewSut(fake, audit);

        var id = await sut.CreaAsync(Riga());

        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Creazione, voce.Operazione);
        Assert.Equal("AttivitaConsulente", voce.EntityType);
        Assert.Equal(id, voce.EntityId);
    }

    [Fact]
    public async Task CreaAsync_AttivitaVuota_Lancia()
    {
        var sut = NewSut(new FakeAttivitaConsulenteRepository());
        var ex = await Assert.ThrowsAsync<AttivitaConsulenteInvalidaException>(
            () => sut.CreaAsync(Riga(idAttivita: Guid.Empty)));
        Assert.Equal(AttivitaConsulenteInvalidaMotivo.AttivitaObbligatoria, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_ConsulenteVuoto_Lancia()
    {
        var sut = NewSut(new FakeAttivitaConsulenteRepository());
        var ex = await Assert.ThrowsAsync<AttivitaConsulenteInvalidaException>(
            () => sut.CreaAsync(Riga(idConsulente: Guid.Empty)));
        Assert.Equal(AttivitaConsulenteInvalidaMotivo.ConsulenteObbligatorio, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_TipoVuoto_Lancia()
    {
        var sut = NewSut(new FakeAttivitaConsulenteRepository());
        var ex = await Assert.ThrowsAsync<AttivitaConsulenteInvalidaException>(
            () => sut.CreaAsync(Riga(idTipo: Guid.Empty)));
        Assert.Equal(AttivitaConsulenteInvalidaMotivo.TipoObbligatorio, ex.Motivo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task CreaAsync_ImportoNonPositivo_Lancia(decimal importo)
    {
        var sut = NewSut(new FakeAttivitaConsulenteRepository());
        var ex = await Assert.ThrowsAsync<AttivitaConsulenteInvalidaException>(
            () => sut.CreaAsync(Riga(importo: importo)));
        Assert.Equal(AttivitaConsulenteInvalidaMotivo.ImportoNonPositivo, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_NotaVuota_NormalizzataANull()
    {
        var fake = new FakeAttivitaConsulenteRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Riga(nota: "   "));
        Assert.Null((await fake.GetByIdAsync(id))!.Nota);
    }

    [Fact]
    public async Task CreaAsync_NotaConSpazi_Trimmata()
    {
        var fake = new FakeAttivitaConsulenteRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Riga(nota: "  urgente  "));
        Assert.Equal("urgente", (await fake.GetByIdAsync(id))!.Nota);
    }

    // ─── Modifica ────────────────────────────────────────────────────────

    [Fact]
    public async Task AggiornaAsync_SenzaPagamenti_AggiornaEAudita()
    {
        var fake = new FakeAttivitaConsulenteRepository();
        var audit = new FakeAuditManager();
        var sut = NewSut(fake, audit);
        var id = await sut.CreaAsync(Riga(importo: 4000m));
        audit.Voci.Clear();

        var mod = Riga(importo: 6000m, carico: CaricoConsulenza.Cliente);
        mod.IdAttivitaConsulente = id;
        await sut.AggiornaAsync(mod);

        var r = await fake.GetByIdAsync(id);
        Assert.Equal(6000m, r!.Importo);
        Assert.Equal(CaricoConsulenza.Cliente, r.Carico);
        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Modifica, voce.Operazione);
    }

    [Fact]
    public async Task AggiornaAsync_ImportoSottoIlPagato_Lancia()
    {
        var fake = new FakeAttivitaConsulenteRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Riga(importo: 5000m));
        fake.PagatoPerRiga[id] = 2000m;

        var mod = Riga(importo: 1500m);   // < 2000 già pagati → residuo negativo
        mod.IdAttivitaConsulente = id;
        var ex = await Assert.ThrowsAsync<AttivitaConsulenteInvalidaException>(() => sut.AggiornaAsync(mod));
        Assert.Equal(AttivitaConsulenteInvalidaMotivo.ImportoInferiorePagato, ex.Motivo);
        Assert.Equal(5000m, (await fake.GetByIdAsync(id))!.Importo);   // invariato
    }

    [Fact]
    public async Task AggiornaAsync_ImportoParialPagato_Ammesso()
    {
        var fake = new FakeAttivitaConsulenteRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Riga(importo: 5000m));
        fake.PagatoPerRiga[id] = 2000m;

        var mod = Riga(importo: 2000m);   // residuo esattamente zero: ok
        mod.IdAttivitaConsulente = id;
        await sut.AggiornaAsync(mod);
        Assert.Equal(2000m, (await fake.GetByIdAsync(id))!.Importo);
    }

    [Fact]
    public async Task AggiornaAsync_CaricoACliente_ConPagamenti_Lancia()
    {
        var fake = new FakeAttivitaConsulenteRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Riga(importo: 5000m, carico: CaricoConsulenza.Studio));
        fake.PagatoPerRiga[id] = 2000m;

        var mod = Riga(importo: 5000m, carico: CaricoConsulenza.Cliente);
        mod.IdAttivitaConsulente = id;
        var ex = await Assert.ThrowsAsync<AttivitaConsulenteInvalidaException>(() => sut.AggiornaAsync(mod));
        Assert.Equal(AttivitaConsulenteInvalidaMotivo.CaricoConPagamenti, ex.Motivo);
        Assert.Equal(CaricoConsulenza.Studio, (await fake.GetByIdAsync(id))!.Carico);   // invariato
    }

    // ─── Eliminazione (D-C2) ─────────────────────────────────────────────

    [Fact]
    public async Task EliminaAsync_ConPagamenti_Lancia_NonDisattiva()
    {
        var fake = new FakeAttivitaConsulenteRepository();
        var sut = NewSut(fake);
        var id = await sut.CreaAsync(Riga());
        fake.PagatoPerRiga[id] = 1000m;

        await Assert.ThrowsAsync<AttivitaConsulenteConPagamentiException>(() => sut.EliminaAsync(id));
        Assert.True((await fake.GetByIdAsync(id))!.IsAttivo);
    }

    [Fact]
    public async Task EliminaAsync_SenzaPagamenti_DisattivaEAudita()
    {
        var fake = new FakeAttivitaConsulenteRepository();
        var audit = new FakeAuditManager();
        var sut = NewSut(fake, audit);
        var id = await sut.CreaAsync(Riga());
        audit.Voci.Clear();

        await sut.EliminaAsync(id);

        Assert.False((await fake.GetByIdAsync(id))!.IsAttivo);
        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Eliminazione, voce.Operazione);
    }

    // ─── Elenco ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ElencoPerAttivitaAsync_SoloRigheDellAttivitaAttive()
    {
        var fake = new FakeAttivitaConsulenteRepository();
        var sut = NewSut(fake);
        var altraAttivita = Guid.NewGuid();
        var id1 = await sut.CreaAsync(Riga());
        await sut.CreaAsync(Riga(idAttivita: altraAttivita));
        var id3 = await sut.CreaAsync(Riga(carico: CaricoConsulenza.Cliente));
        await sut.EliminaAsync(id3);

        var elenco = await sut.ElencoPerAttivitaAsync(IdAttivita);

        var unica = Assert.Single(elenco);
        Assert.Equal(id1, unica.IdAttivitaConsulente);
    }
}
