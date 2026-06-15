using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;
using ICMFatturazioni.Web.Manutenzione;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test della logica di manutenzione dell'audit (<c>AuditManutenzione</c>):
///   1) la retention temporale elimina SOLO l'audit oltre la finestra;
///   2) la sentinella sulla dimensione emette un Warning oltre soglia e tace sotto;
///   3) l'esito riporta righe eliminate, dimensione e stato dell'allarme.
/// Usa l'<c>AuditManager</c> reale su un repository fake, così il calcolo
/// mesi → soglia viene esercitato davvero.
/// </summary>
public class AuditManutenzioneTests
{
    private static readonly DateTimeOffset Adesso = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private static (AuditManutenzione sut, FakeAuditRepository repo, FakeDatabaseSizeRepository size, CapturingLogger<AuditManutenzione> log)
        NewSut(int mesi = 36, int sogliaGb = 8)
    {
        var clock = new TestTimeProvider(Adesso);
        var repo = new FakeAuditRepository();
        var auditManager = new AuditManager(repo, new FakeCurrentUserAccessor(), new FakeLogManager(), clock);
        var size = new FakeDatabaseSizeRepository();
        var options = Options.Create(new AuditRetentionOptions { MesiConservazione = mesi, SogliaAllarmeGb = sogliaGb });
        var log = new CapturingLogger<AuditManutenzione>();
        return (new AuditManutenzione(auditManager, size, options, log), repo, size, log);
    }

    private static Audit Riga(DateTime timestampUtc) => new()
    {
        Id = Guid.NewGuid(),
        TimestampUtc = timestampUtc,
        Operazione = AuditOperazione.Creazione,
        EntityType = "Anagrafica",
    };

    [Fact]
    public async Task EseguiAsync_EliminaSoloAuditOltreLaFinestraDiRetention()
    {
        var (sut, repo, size, _) = NewSut(mesi: 36);
        size.DimensioneDatiMb = 100;
        var adessoUtc = Adesso.UtcDateTime;
        await repo.InsertAsync(Riga(adessoUtc.AddMonths(-40)));   // oltre i 36 mesi → eliminata
        await repo.InsertAsync(Riga(adessoUtc.AddMonths(-1)));    // recente → conservata

        var esito = await sut.EseguiAsync();

        Assert.Equal(1, esito.RigheEliminate);
        Assert.Single(repo.Inseriti);
        Assert.Equal(adessoUtc.AddMonths(-1), repo.Inseriti[0].TimestampUtc);
    }

    [Fact]
    public async Task EseguiAsync_SottoSoglia_NonEmetteAllarme()
    {
        var (sut, _, size, log) = NewSut(sogliaGb: 8);
        size.DimensioneDatiMb = 4096;   // 4 GB < 8 GB

        var esito = await sut.EseguiAsync();

        Assert.False(esito.AllarmeEmesso);
        Assert.Equal(4096, esito.DimensioneDatiMb);
        Assert.DoesNotContain(log.Voci, v => v.Livello == LogLevel.Warning);
    }

    [Fact]
    public async Task EseguiAsync_SopraSoglia_EmetteWarning()
    {
        var (sut, _, size, log) = NewSut(sogliaGb: 8);
        size.DimensioneDatiMb = 9000;   // ~8,8 GB >= 8 GB

        var esito = await sut.EseguiAsync();

        Assert.True(esito.AllarmeEmesso);
        Assert.Single(log.Voci, v => v.Livello == LogLevel.Warning);
    }

    [Fact]
    public async Task EseguiAsync_EsattamenteASoglia_EmetteWarning()
    {
        var (sut, _, size, _) = NewSut(sogliaGb: 8);
        size.DimensioneDatiMb = 8 * 1024;   // esattamente 8 GB → allarme (>=)

        var esito = await sut.EseguiAsync();

        Assert.True(esito.AllarmeEmesso);
    }

    [Theory]
    [InlineData(4096, false)]   // 4 GB < 8 GB
    [InlineData(9000, true)]    // ~8,8 GB >= 8 GB
    public async Task ValutaDimensioneAsync_RestituisceLoStatoSenzaEffetti(int mb, bool allarmeAtteso)
    {
        var (sut, repo, size, log) = NewSut(sogliaGb: 8);
        size.DimensioneDatiMb = mb;
        await repo.InsertAsync(Riga(Adesso.UtcDateTime.AddMonths(-40)));   // riga vecchia "civetta"

        var stato = await sut.ValutaDimensioneAsync();

        Assert.Equal(mb, stato.DimensioneDatiMb);
        Assert.Equal(8, stato.SogliaGb);
        Assert.Equal(allarmeAtteso, stato.Allarme);
        // Sola lettura: niente purga (la riga vecchia resta) e niente log.
        Assert.Single(repo.Inseriti);
        Assert.Empty(log.Voci);
    }
}
