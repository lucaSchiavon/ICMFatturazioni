using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;
using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test del manager Fatture: numerazione progressiva annuale, creazione da un avviso
/// con guardie (avviso mancante/già fatturato, data/numero non validi, numero
/// duplicato), annullamento con riapertura dell'avviso, e audit.
/// </summary>
public class FattureManagerTests
{
    private sealed class Sut
    {
        public required FattureManager           Manager { get; init; }
        public required FakeFattureRepository     Fatture { get; init; }
        public required FakeAvvisoFatturaRepository Avvisi { get; init; }
        public required FakeAuditManager          Audit   { get; init; }
    }

    private static Sut NewSut()
    {
        var fatture = new FakeFattureRepository();
        var avvisi  = new FakeAvvisoFatturaRepository();
        var audit   = new FakeAuditManager();
        return new Sut
        {
            Manager = new FattureManager(fatture, avvisi, audit),
            Fatture = fatture,
            Avvisi  = avvisi,
            Audit   = audit,
        };
    }

    // Semina un avviso attivo direttamente nel fake e ne restituisce l'Id.
    private static Guid SeedAvviso(FakeAvvisoFatturaRepository avvisi)
    {
        var id = Guid.NewGuid();
        avvisi.Seed(new AvvisoFattura
        {
            IdAvviso     = id,
            IdAttivita   = Guid.NewGuid(),
            IdAnagrafica = Guid.NewGuid(),
            DataAvviso   = new DateOnly(2026, 7, 1),
            IsAttivo     = true,
        });
        return id;
    }

    private static CreaFatturaRequest Req(Guid idAvviso, int numero = 1, DateOnly? data = null)
        => new(idAvviso, numero, data ?? new DateOnly(2026, 7, 3));

    // =====================================================================
    // Numerazione
    // =====================================================================

    [Fact]
    public async Task ProponiNumeroAsync_AnnoVuoto_RestituisceUno()
        => Assert.Equal(1, await NewSut().Manager.ProponiNumeroAsync(2026));

    [Fact]
    public async Task ProponiNumeroAsync_DopoCreazione_IncrementaSullUltimoDellAnno()
    {
        var sut = NewSut();
        var a1 = SeedAvviso(sut.Avvisi);
        await sut.Manager.CreaAsync(Req(a1, numero: 37, data: new DateOnly(2026, 7, 3)));

        Assert.Equal(38, await sut.Manager.ProponiNumeroAsync(2026));
        // Anno diverso: numerazione indipendente.
        Assert.Equal(1, await sut.Manager.ProponiNumeroAsync(2027));
    }

    // =====================================================================
    // Creazione — happy path
    // =====================================================================

    [Fact]
    public async Task CreaAsync_Valido_CreaFatturaEAudita()
    {
        var sut = NewSut();
        var idAvviso = SeedAvviso(sut.Avvisi);

        var idFattura = await sut.Manager.CreaAsync(Req(idAvviso, numero: 37, data: new DateOnly(2026, 7, 3)));

        var fattura = await sut.Manager.GetAttivaByAvvisoAsync(idAvviso);
        Assert.NotNull(fattura);
        Assert.Equal(idFattura, fattura!.IdFattura);
        Assert.Equal(37, fattura.NumeroFattura);
        Assert.Equal(2026, fattura.Anno);          // derivato dalla data
        Assert.True(fattura.IsAttivo);

        var voce = Assert.Single(sut.Audit.Voci);
        Assert.Equal(AuditOperazione.Creazione, voce.Operazione);
        Assert.Equal("Fattura", voce.EntityType);
        Assert.Equal(idFattura, voce.EntityId);
    }

    // =====================================================================
    // Creazione — validazioni / guardie
    // =====================================================================

    [Fact]
    public async Task CreaAsync_AvvisoInesistente_Lancia()
    {
        var sut = NewSut();
        var ex = await Assert.ThrowsAsync<FatturaInvalidaException>(
            () => sut.Manager.CreaAsync(Req(Guid.NewGuid())));
        Assert.Equal(FatturaMotivoInvalido.AvvisoNonTrovato, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_AvvisoGiaFatturato_Lancia()
    {
        var sut = NewSut();
        var idAvviso = SeedAvviso(sut.Avvisi);
        await sut.Manager.CreaAsync(Req(idAvviso, numero: 1));

        var ex = await Assert.ThrowsAsync<FatturaInvalidaException>(
            () => sut.Manager.CreaAsync(Req(idAvviso, numero: 2)));
        Assert.Equal(FatturaMotivoInvalido.AvvisoGiaFatturato, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_DataDefault_Lancia()
    {
        var sut = NewSut();
        var idAvviso = SeedAvviso(sut.Avvisi);
        var ex = await Assert.ThrowsAsync<FatturaInvalidaException>(
            () => sut.Manager.CreaAsync(new CreaFatturaRequest(idAvviso, 1, default)));
        Assert.Equal(FatturaMotivoInvalido.DataObbligatoria, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_NumeroNonPositivo_Lancia()
    {
        var sut = NewSut();
        var idAvviso = SeedAvviso(sut.Avvisi);
        var ex = await Assert.ThrowsAsync<FatturaInvalidaException>(
            () => sut.Manager.CreaAsync(Req(idAvviso, numero: 0)));
        Assert.Equal(FatturaMotivoInvalido.NumeroNonValido, ex.Motivo);
    }

    [Fact]
    public async Task CreaAsync_NumeroDuplicatoNellAnno_Lancia()
    {
        var sut = NewSut();
        var a1 = SeedAvviso(sut.Avvisi);
        var a2 = SeedAvviso(sut.Avvisi);
        await sut.Manager.CreaAsync(Req(a1, numero: 5, data: new DateOnly(2026, 7, 3)));

        var ex = await Assert.ThrowsAsync<FatturaInvalidaException>(
            () => sut.Manager.CreaAsync(Req(a2, numero: 5, data: new DateOnly(2026, 8, 1))));
        Assert.Equal(FatturaMotivoInvalido.NumeroDuplicato, ex.Motivo);
    }

    // =====================================================================
    // Annullamento
    // =====================================================================

    [Fact]
    public async Task AnnullaAsync_SoftDeleteRiapreLAvvisoEAudita()
    {
        var sut = NewSut();
        var idAvviso = SeedAvviso(sut.Avvisi);
        var idFattura = await sut.Manager.CreaAsync(Req(idAvviso, numero: 10));
        sut.Audit.Voci.Clear();

        await sut.Manager.AnnullaAsync(idFattura);

        // L'avviso torna non fatturato: nessuna fattura attiva collegata.
        Assert.Null(await sut.Manager.GetAttivaByAvvisoAsync(idAvviso));
        var voce = Assert.Single(sut.Audit.Voci);
        Assert.Equal(AuditOperazione.Eliminazione, voce.Operazione);
    }

    [Fact]
    public async Task AnnullaAsync_PermetteRifatturareLAvviso()
    {
        var sut = NewSut();
        var idAvviso = SeedAvviso(sut.Avvisi);
        var id1 = await sut.Manager.CreaAsync(Req(idAvviso, numero: 10));
        await sut.Manager.AnnullaAsync(id1);

        // L'avviso è di nuovo fatturabile: numero riutilizzabile.
        var id2 = await sut.Manager.CreaAsync(Req(idAvviso, numero: 10));
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public async Task AnnullaAsync_Inesistente_NoOp()
    {
        var sut = NewSut();
        await sut.Manager.AnnullaAsync(Guid.NewGuid());
        Assert.Empty(sut.Audit.Voci);
    }
}
