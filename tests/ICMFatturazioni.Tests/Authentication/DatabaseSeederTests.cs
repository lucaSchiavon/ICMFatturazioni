using ICMFatturazioni.Web.Authentication;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Repositories.Interfaces;
using ICMFatturazioni.Tests.Managers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ICMFatturazioni.Tests.Authentication;

/// <summary>
/// Test del <c>DatabaseSeeder</c> (IHostedService di seed idempotente). Verifica:
///   1) skip se la password non è configurata (nessun utente creato);
///   2) creazione dell'Admin quando password e ruolo di sistema esistono;
///   3) idempotenza: se l'utente esiste già, non viene ricreato;
///   4) ruolo di sistema mancante → l'errore è gestito e loggato, StartAsync non
///      rilancia (l'avvio dell'app non si interrompe).
/// Si costruisce un vero <see cref="ServiceProvider"/> per esercitare lo scope DI.
/// </summary>
public class DatabaseSeederTests
{
    private sealed class Harness
    {
        public required ServiceProvider Provider { get; init; }
        public required FakeUtenteRepository Utenti { get; init; }
        public required FakeRuoloRepository Ruoli { get; init; }
        public required FakeLogManager Logger { get; init; }
    }

    /// <summary>Compone il provider con i fake condivisi (ispezionabili dopo il seed).</summary>
    private static Harness BuildHarness()
    {
        var utenti = new FakeUtenteRepository();
        var ruoli = new FakeRuoloRepository();
        var logger = new FakeLogManager();

        var services = new ServiceCollection();
        services.AddSingleton<IUtenteRepository>(utenti);
        services.AddSingleton<IRuoloRepository>(ruoli);
        services.AddSingleton<IPasswordHasherService, PasswordHasherService>();
        services.AddSingleton<ILogManager>(logger);
        services.AddSingleton<IAuditManager>(new FakeAuditManager());
        services.AddScoped<IUtenteManager, UtenteManager>();
        services.AddScoped<IRuoloManager, RuoloManager>();

        return new Harness
        {
            Provider = services.BuildServiceProvider(),
            Utenti = utenti,
            Ruoli = ruoli,
            Logger = logger,
        };
    }

    private static void SeedRuoloAdmin(FakeRuoloRepository ruoli)
        => ruoli.Seed(new Ruolo
        {
            IdRuolo = Guid.NewGuid(),
            Codice = RuoliSistema.Admin,
            Nome = "Amministratore",
            IsSistema = true,
        });

    private static DatabaseSeeder NewSeeder(Harness h, AdminSeederOptions admin, SuperadminSeederOptions? super = null)
        => new(h.Provider, Options.Create(admin), Options.Create(super ?? new SuperadminSeederOptions()));

    [Fact]
    public async Task StartAsync_PasswordNonConfigurata_NonCreaNessunUtente()
    {
        var h = BuildHarness();
        SeedRuoloAdmin(h.Ruoli);
        // Admin senza password e Superadmin senza password (default).
        var seeder = NewSeeder(h, new AdminSeederOptions { DefaultPassword = null });

        await seeder.StartAsync(CancellationToken.None);

        Assert.Empty(h.Utenti.Store);
        Assert.Empty(h.Logger.Errori);   // nessun errore: skip pulito
    }

    [Fact]
    public async Task StartAsync_AdminConfigurato_CreaLUtenteAdmin()
    {
        var h = BuildHarness();
        SeedRuoloAdmin(h.Ruoli);
        var seeder = NewSeeder(h, new AdminSeederOptions
        {
            DefaultUsername = "admin",
            DefaultPassword = "Admin12345",
        });

        await seeder.StartAsync(CancellationToken.None);

        var creato = await h.Utenti.GetByUsernameAsync("admin");
        Assert.NotNull(creato);
        Assert.NotNull(creato!.PasswordHash);
        Assert.Empty(h.Logger.Errori);
    }

    [Fact]
    public async Task StartAsync_AdminGiaEsistente_NonLoRicrea()
    {
        var h = BuildHarness();
        var ruolo = h.Ruoli.Seed(new Ruolo { IdRuolo = Guid.NewGuid(), Codice = RuoliSistema.Admin, Nome = "Amministratore", IsSistema = true });
        // Pre-creo l'admin a mano.
        await h.Utenti.InsertAsync(new Utente
        {
            IdUtente = Guid.CreateVersion7(),
            Username = "admin",
            PasswordHash = "hash-preesistente",
            IdRuolo = ruolo.IdRuolo,
            Attivo = true,
        });
        var seeder = NewSeeder(h, new AdminSeederOptions { DefaultUsername = "admin", DefaultPassword = "Admin12345" });

        await seeder.StartAsync(CancellationToken.None);

        Assert.Single(h.Utenti.Store);
        Assert.Equal("hash-preesistente", (await h.Utenti.GetByUsernameAsync("admin"))!.PasswordHash);
    }

    [Fact]
    public async Task StartAsync_RuoloDiSistemaMancante_LoggaErroreGestitoSenzaRilanciare()
    {
        var h = BuildHarness();
        // NESSUN ruolo ADMIN nel repository.
        var seeder = NewSeeder(h, new AdminSeederOptions { DefaultUsername = "admin", DefaultPassword = "Admin12345" });

        // Non deve lanciare: l'eccezione è catturata in StartAsync.
        await seeder.StartAsync(CancellationToken.None);

        Assert.Empty(h.Utenti.Store);
        Assert.Single(h.Logger.Errori);
        Assert.IsType<InvalidOperationException>(h.Logger.Errori[0].Eccezione);
    }

    [Fact]
    public async Task StartAsync_SuperadminConfigurato_CreaAncheIlSuperadmin()
    {
        var h = BuildHarness();
        SeedRuoloAdmin(h.Ruoli);
        h.Ruoli.Seed(new Ruolo { IdRuolo = Guid.NewGuid(), Codice = RuoliSistema.Superadmin, Nome = "Superadmin", IsSistema = true });
        var seeder = NewSeeder(h,
            new AdminSeederOptions { DefaultUsername = "admin", DefaultPassword = "Admin12345" },
            new SuperadminSeederOptions { Username = "super", Password = "Super1pass9" });

        await seeder.StartAsync(CancellationToken.None);

        Assert.NotNull(await h.Utenti.GetByUsernameAsync("admin"));
        Assert.NotNull(await h.Utenti.GetByUsernameAsync("super"));
    }
}
