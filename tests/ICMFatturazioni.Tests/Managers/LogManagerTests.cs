using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;
using ICMFatturazioni.Web.Models;

namespace ICMFatturazioni.Tests.Managers;

/// <summary>
/// Test del <c>LogManager</c> (path esplicito di logging errori). Verifica:
///   1) l'errore è persistito a livello Error con i campi valorizzati;
///   2) i segreti in messaggio/stack/spiegazione sono sanitizzati (Regola 6);
///   3) se la scrittura su DB fallisce, ricade sul fallback SENZA rilanciare
///      (un logger non deve mai mascherare l'errore originale).
/// </summary>
public class LogManagerTests
{
    private static (LogManager sut, FakeLogRepository repo, FakeLogFallbackWriter fallback) NewSut()
    {
        var repo = new FakeLogRepository();
        var fallback = new FakeLogFallbackWriter();
        return (new LogManager(repo, fallback, TimeProvider.System), repo, fallback);
    }

    [Fact]
    public async Task LogErroreAsync_PersisteConLivelloErrorEContesto()
    {
        var (sut, repo, fallback) = NewSut();
        var utente = Guid.NewGuid();
        var entita = Guid.NewGuid();

        await sut.LogErroreAsync(new InvalidOperationException("boom"),
            "Spiegazione leggibile.", "Test.Sorgente",
            utenteId: utente, entityId: entita, entityType: "Anagrafica");

        var log = Assert.Single(repo.Inseriti);
        Assert.Equal(LogLivello.Error, log.Livello);
        Assert.Equal("Test.Sorgente", log.Sorgente);
        Assert.Equal("boom", log.Messaggio);
        Assert.Equal("Spiegazione leggibile.", log.SpiegazioneUtente);
        Assert.Equal(typeof(InvalidOperationException).FullName, log.EccezioneTipo);
        Assert.Equal(utente, log.UtenteId);
        Assert.Equal(entita, log.EntityId);
        Assert.Equal("Anagrafica", log.EntityType);
        Assert.NotEqual(Guid.Empty, log.Id);           // GUID v7 generato
        Assert.Empty(fallback.Scritture);              // DB ok → niente fallback
    }

    [Fact]
    public async Task LogErroreAsync_SanitizzaSegretiNelMessaggio()
    {
        var (sut, repo, _) = NewSut();
        var ex = new Exception("Login fallito; Password=SuperSegreta123; Server=db01");

        await sut.LogErroreAsync(ex, "Token=abc.def nel testo", "Test");

        var log = Assert.Single(repo.Inseriti);
        Assert.DoesNotContain("SuperSegreta123", log.Messaggio);
        Assert.Contains("Password=***", log.Messaggio);
        Assert.DoesNotContain("db01", log.Messaggio);
        // Anche la spiegazione passa dal sanitizer.
        Assert.DoesNotContain("abc.def", log.SpiegazioneUtente!);
        Assert.Contains("Token=***", log.SpiegazioneUtente!);
    }

    [Fact]
    public async Task LogErroreAsync_SeDbFallisce_RicadeSuFallbackSenzaRilanciare()
    {
        var (sut, repo, fallback) = NewSut();
        repo.ThrowOnInsert = true;   // DB irraggiungibile

        // Non deve propagare: la chiamata completa senza eccezione.
        await sut.LogErroreAsync(new Exception("x"), "spiegazione", "Test");

        Assert.Empty(repo.Inseriti);
        var scrittura = Assert.Single(fallback.Scritture);
        Assert.Equal("Test", scrittura.Entry.Sorgente);
        Assert.NotNull(scrittura.Causa);   // l'errore di scrittura è tracciato
    }

    [Fact]
    public async Task CercaAsync_DelegaAlRepositoryEApplicaIFiltri()
    {
        var (sut, repo, _) = NewSut();
        repo.Inseriti.Add(new Log { Id = Guid.NewGuid(), TimestampUtc = DateTime.UtcNow, Livello = LogLivello.Error, Sorgente = "A", Messaggio = "uno" });
        repo.Inseriti.Add(new Log { Id = Guid.NewGuid(), TimestampUtc = DateTime.UtcNow, Livello = LogLivello.Warning, Sorgente = "B", Messaggio = "due" });

        var ris = await sut.CercaAsync(new LogFiltro { Livello = LogLivello.Error });

        Assert.Equal(1, ris.TotaleRighe);
        Assert.Equal("uno", ris.Righe[0].Messaggio);
    }

    [Fact]
    public async Task PurgaPrecedentiAsync_EliminaSoloLeRigheOltreLaSoglia()
    {
        var (sut, repo, _) = NewSut();
        repo.Inseriti.Add(new Log { Id = Guid.NewGuid(), TimestampUtc = DateTime.UtcNow.AddDays(-100), Sorgente = "x", Messaggio = "vecchio" });
        repo.Inseriti.Add(new Log { Id = Guid.NewGuid(), TimestampUtc = DateTime.UtcNow, Sorgente = "x", Messaggio = "nuovo" });

        var eliminati = await sut.PurgaPrecedentiAsync(90);

        Assert.Equal(1, eliminati);
        Assert.Equal("nuovo", Assert.Single(repo.Inseriti).Messaggio);
    }
}
