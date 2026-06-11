using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;
using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test dell'<c>AuditManager</c> ("chi-ha-fatto-cosa"). Verifica:
///   1) lo snapshot di id+nome dell'utente corrente e l'operazione corretta;
///   2) la descrizione viene troncata alla capacità della colonna (512);
///   3) un fallimento di scrittura è best-effort: viene loggato e NON propagato
///      (l'operazione di business è già avvenuta).
/// </summary>
public class AuditManagerTests
{
    private static readonly Guid Operatore = Guid.Parse("e0000000-0000-0000-0000-000000000001");
    private static readonly Guid Entita = Guid.Parse("f0000000-0000-0000-0000-000000000001");

    private static AuditManager NewSut(
        FakeAuditRepository repo, Guid? utenteId = null, string? utenteNome = null, FakeLogManager? log = null)
        => new(repo, new FakeCurrentUserAccessor(utenteId, utenteNome), log ?? new FakeLogManager(), TimeProvider.System);

    [Fact]
    public async Task RegistraCreazioneAsync_CatturaSnapshotUtenteEOperazione()
    {
        var repo = new FakeAuditRepository();
        var sut = NewSut(repo, Operatore, "mario.rossi");

        await sut.RegistraCreazioneAsync("Anagrafica", Entita, "Acme S.p.A.");

        var a = Assert.Single(repo.Inseriti);
        Assert.Equal(AuditOperazione.Creazione, a.Operazione);
        Assert.Equal(Operatore, a.UtenteId);
        Assert.Equal("mario.rossi", a.UtenteNome);     // snapshot del nome
        Assert.Equal("Anagrafica", a.EntityType);
        Assert.Equal(Entita, a.EntityId);
        Assert.Equal("Acme S.p.A.", a.Descrizione);
        Assert.NotEqual(Guid.Empty, a.Id);
    }

    [Theory]
    [InlineData(nameof(AuditOperazione.Modifica))]
    [InlineData(nameof(AuditOperazione.Eliminazione))]
    public async Task Registra_ImpostaLOperazioneCorretta(string operazione)
    {
        var repo = new FakeAuditRepository();
        var sut = NewSut(repo, Operatore, "admin");

        Task azione = operazione == nameof(AuditOperazione.Modifica)
            ? sut.RegistraModificaAsync("Utente", Entita, "x")
            : sut.RegistraEliminazioneAsync("Utente", Entita, "x");
        await azione;

        Assert.Equal(Enum.Parse<AuditOperazione>(operazione), repo.Inseriti[0].Operazione);
    }

    [Fact]
    public async Task RegistraAsync_UtenteAnonimo_RegistraConUtenteNull()
    {
        var repo = new FakeAuditRepository();
        var sut = NewSut(repo);   // nessun utente corrente (flusso anonimo)

        await sut.RegistraCreazioneAsync("UtenteToken", Entita, "reset");

        Assert.Null(repo.Inseriti[0].UtenteId);
        Assert.Null(repo.Inseriti[0].UtenteNome);
    }

    [Fact]
    public async Task RegistraAsync_DescrizioneTroppoLunga_VieneTroncataA512()
    {
        var repo = new FakeAuditRepository();
        var sut = NewSut(repo, Operatore, "admin");
        var lunga = new string('x', 600);

        await sut.RegistraModificaAsync("Anagrafica", Entita, lunga);

        Assert.Equal(512, repo.Inseriti[0].Descrizione!.Length);
    }

    [Fact]
    public async Task RegistraAsync_SeScritturaFallisce_LoggaENonRilancia()
    {
        var repo = new FakeAuditRepository { ThrowOnInsert = true };
        var log = new FakeLogManager();
        var sut = NewSut(repo, Operatore, "admin", log);

        // Best-effort: non deve propagare il fallimento dell'audit.
        await sut.RegistraEliminazioneAsync("Anagrafica", Entita, "x");

        Assert.Empty(repo.Inseriti);
        var voce = Assert.Single(log.Errori);          // il fallimento è tracciato
        Assert.Equal("AuditManager.RegistraAsync", voce.Sorgente);
    }

    [Fact]
    public async Task CercaAsync_DelegaAlRepositoryEApplicaIFiltri()
    {
        var repo = new FakeAuditRepository();
        var sut = NewSut(repo, Operatore, "admin");
        await sut.RegistraCreazioneAsync("Anagrafica", Entita, "x");
        await sut.RegistraEliminazioneAsync("Utente", Entita, "y");

        var ris = await sut.CercaAsync(new AuditFiltro { Operazione = AuditOperazione.Eliminazione });

        Assert.Equal(1, ris.TotaleRighe);
        Assert.Equal(AuditOperazione.Eliminazione, ris.Righe[0].Operazione);
    }

    [Fact]
    public async Task GetEntityTypesAsync_RestituisceITipiDistintiOrdinati()
    {
        var repo = new FakeAuditRepository();
        var sut = NewSut(repo, Operatore, "admin");
        await sut.RegistraCreazioneAsync("Utente", Entita, null);
        await sut.RegistraModificaAsync("Anagrafica", Entita, null);
        await sut.RegistraCreazioneAsync("Anagrafica", Entita, null);

        var tipi = await sut.GetEntityTypesAsync();

        Assert.Equal(new[] { "Anagrafica", "Utente" }, tipi);
    }
}
