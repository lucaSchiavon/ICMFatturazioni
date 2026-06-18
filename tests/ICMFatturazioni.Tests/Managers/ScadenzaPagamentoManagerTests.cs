using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test del manager ScadenzaPagamento: validazione campi, protezione HasFattura, audit.
/// </summary>
public class ScadenzaPagamentoManagerTests
{
    private static readonly Guid IdDettaglio = Guid.NewGuid();

    private static ScadenzaPagamento Sca(
        DateOnly? data    = null,
        decimal   importo = 500m,
        Guid?     idDett  = null) => new()
    {
        IdAttivitaDettaglio = idDett ?? IdDettaglio,
        DataScadenza        = data   ?? new DateOnly(2025, 12, 31),
        Importo             = importo,
    };

    private static (ScadenzaPagamentoManager sut, FakeScadenzaPagamentoRepository fakeRepo, FakeAttivitaDettaglioRepository fakeDett)
        NewSut(FakeAuditManager? audit = null)
    {
        var fakeRepo = new FakeScadenzaPagamentoRepository();
        var fakeDett = new FakeAttivitaDettaglioRepository();
        // Pre-popola il dettaglio parent in modo che il Manager lo trovi.
        fakeDett.InsertAsync(new AttivitaDettaglio
        {
            IdAttivitaDettaglio     = IdDettaglio,
            IdAttivita              = Guid.NewGuid(),
            IdTipoDettaglioAttivita = Guid.NewGuid(),
            Ordine                  = 1,
            DescrizioneDettaglio    = "Disciplinare",
            Importo                 = 1000m,
        }).Wait();
        var sut = new ScadenzaPagamentoManager(fakeRepo, fakeDett, audit ?? new FakeAuditManager());
        return (sut, fakeRepo, fakeDett);
    }

    // -------------------------------------------------------------------------
    // Creazione
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreaAsync_Valido_RestituisceId()
    {
        var (sut, fakeRepo, _) = NewSut();

        var id = await sut.CreaAsync(Sca());

        Assert.NotEqual(Guid.Empty, id);
        var s = await fakeRepo.GetByIdAsync(id);
        Assert.NotNull(s);
        Assert.Equal(500m, s.Importo);
    }

    [Fact]
    public async Task CreaAsync_RegistraAuditCreazione()
    {
        var audit = new FakeAuditManager();
        var (sut, _, _) = NewSut(audit);

        var id = await sut.CreaAsync(Sca());

        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Creazione, voce.Operazione);
        Assert.Equal("ScadenzaPagamento", voce.EntityType);
        Assert.Equal(id, voce.EntityId);
    }

    // -------------------------------------------------------------------------
    // Validazione campi
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreaAsync_DataDefault_LanciaDataObbligatoria()
    {
        var (sut, _, _) = NewSut();
        // DataScadenza non impostata: vale default(DateOnly) = 0001-01-01, considerata assente.
        var scadenza = new ScadenzaPagamento
        {
            IdAttivitaDettaglio = IdDettaglio,
            Importo             = 500m,
        };
        var ex = await Assert.ThrowsAsync<ScadenzaPagamentoInvalidaException>(
            () => sut.CreaAsync(scadenza));
        Assert.Equal(ScadenzaPagamentoMotivoInvalido.DataObbligatoria, ex.Motivo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task CreaAsync_ImportoNonPositivo_LanciaImportoNonValido(decimal importo)
    {
        var (sut, _, _) = NewSut();
        var ex = await Assert.ThrowsAsync<ScadenzaPagamentoInvalidaException>(
            () => sut.CreaAsync(Sca(importo: importo)));
        Assert.Equal(ScadenzaPagamentoMotivoInvalido.ImportoNonValido, ex.Motivo);
    }

    // -------------------------------------------------------------------------
    // Protezione HasFattura
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreaAsync_DettaglioFatturato_LanciaDettaglioFatturato()
    {
        var (sut, _, fakeDett) = NewSut();
        fakeDett.SetHasFattura(IdDettaglio, true);

        var ex = await Assert.ThrowsAsync<ScadenzaPagamentoInvalidaException>(
            () => sut.CreaAsync(Sca()));
        Assert.Equal(ScadenzaPagamentoMotivoInvalido.DettaglioFatturato, ex.Motivo);
    }

    [Fact]
    public async Task EliminaAsync_DettaglioFatturato_LanciaDettaglioFatturato()
    {
        var (sut, fakeRepo, fakeDett) = NewSut();
        var id = await sut.CreaAsync(Sca());
        fakeDett.SetHasFattura(IdDettaglio, true);

        var ex = await Assert.ThrowsAsync<ScadenzaPagamentoInvalidaException>(
            () => sut.EliminaAsync(id));
        Assert.Equal(ScadenzaPagamentoMotivoInvalido.DettaglioFatturato, ex.Motivo);
        Assert.True((await fakeRepo.GetByIdAsync(id))!.IsAttivo);
    }

    [Fact]
    public async Task EliminaAsync_SenzaFattura_DisattivaEAudita()
    {
        var audit = new FakeAuditManager();
        var (sut, fakeRepo, _) = NewSut(audit);
        var id = await sut.CreaAsync(Sca());
        audit.Voci.Clear();

        await sut.EliminaAsync(id);

        Assert.False((await fakeRepo.GetByIdAsync(id))!.IsAttivo);
        var voce = Assert.Single(audit.Voci);
        Assert.Equal(AuditOperazione.Eliminazione, voce.Operazione);
    }

    // -------------------------------------------------------------------------
    // Elenco
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ElencoPerDettaglioAsync_OrdinaPerDataAsc()
    {
        var (sut, _, _) = NewSut();
        await sut.CreaAsync(Sca(data: new DateOnly(2025, 12, 31)));
        await sut.CreaAsync(Sca(data: new DateOnly(2025,  6, 30)));
        await sut.CreaAsync(Sca(data: new DateOnly(2026,  3, 31)));

        var lista = await sut.ElencoPerDettaglioAsync(IdDettaglio);

        Assert.Equal(3, lista.Count);
        Assert.Equal(new DateOnly(2025,  6, 30), lista[0].DataScadenza);
        Assert.Equal(new DateOnly(2025, 12, 31), lista[1].DataScadenza);
        Assert.Equal(new DateOnly(2026,  3, 31), lista[2].DataScadenza);
    }
}
