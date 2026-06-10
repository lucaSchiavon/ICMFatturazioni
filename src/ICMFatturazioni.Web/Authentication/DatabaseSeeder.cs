using ICMFatturazioni.Web.Diagnostics;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers.Interfaces;
using Microsoft.Extensions.Options;

namespace ICMFatturazioni.Web.Authentication;

/// <summary>
/// Seed idempotente degli utenti Admin e Superadmin all'avvio dell'app
/// (sostituisce il vecchio seed hardcoded <c>admin/admin1234</c>).
/// </summary>
/// <remarks>
/// <para>
/// Le password NON sono nel codice: arrivano da user-secrets (dev) o variabili
/// d'ambiente (prod) tramite <see cref="AdminSeederOptions"/> /
/// <see cref="SuperadminSeederOptions"/>. Se una password non è configurata, il
/// relativo utente non viene creato (skip). In dev, di norma, si seeda solo
/// l'Admin; il Superadmin si crea solo dove serve davvero.
/// </para>
/// <para>
/// È un <see cref="IHostedService"/> (singleton): apre un proprio scope DI per
/// risolvere i servizi scoped (manager). Eventuali errori (es. DB non ancora
/// migrato) vengono loggati via <see cref="IErrorLogger"/> e non bloccano
/// l'avvio.
/// </para>
/// </remarks>
internal sealed class DatabaseSeeder : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly AdminSeederOptions _adminOptions;
    private readonly SuperadminSeederOptions _superadminOptions;

    public DatabaseSeeder(
        IServiceProvider services,
        IOptions<AdminSeederOptions> adminOptions,
        IOptions<SuperadminSeederOptions> superadminOptions)
    {
        _services = services;
        _adminOptions = adminOptions.Value;
        _superadminOptions = superadminOptions.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var utenteManager = scope.ServiceProvider.GetRequiredService<IUtenteManager>();
        var ruoloManager = scope.ServiceProvider.GetRequiredService<IRuoloManager>();
        var errorLogger = scope.ServiceProvider.GetRequiredService<IErrorLogger>();

        try
        {
            await SeedUtenteAsync(
                utenteManager, ruoloManager,
                _adminOptions.DefaultUsername, _adminOptions.DefaultPassword, _adminOptions.DefaultEmail,
                RuoliSistema.Admin, "Amministratore", cancellationToken);

            await SeedUtenteAsync(
                utenteManager, ruoloManager,
                _superadminOptions.Username, _superadminOptions.Password, _superadminOptions.Email,
                RuoliSistema.Superadmin, "Superadmin", cancellationToken);
        }
        catch (Exception ex)
        {
            // Probabile DB non migrato: logghiamo (con fallback su file) e
            // proseguiamo l'avvio. Login fallirà finché non si applicano le migration.
            await errorLogger.LogAsync(
                ex,
                contesto: "DatabaseSeeder.StartAsync",
                descrizioneEstesa: "Seed utenti fallito. Eseguire le migration (execution/recreate-db.ps1).",
                severity: Severity.Warning,
                handled: true,
                cancellationToken: cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Crea un utente con il ruolo di sistema indicato, se: la password è
    /// configurata, l'utente non esiste già e il ruolo è presente. Idempotente.
    /// </summary>
    private static async Task SeedUtenteAsync(
        IUtenteManager utenteManager,
        IRuoloManager ruoloManager,
        string username,
        string? password,
        string? email,
        string codiceRuolo,
        string nomeCompleto,
        CancellationToken cancellationToken)
    {
        // Password non configurata → niente seed per questo utente.
        if (string.IsNullOrEmpty(password))
        {
            return;
        }

        // Idempotenza: se esiste già, non ricreare.
        var esistente = await utenteManager.GetByUsernameAsync(username, cancellationToken);
        if (esistente is not null)
        {
            return;
        }

        var ruolo = await ruoloManager.GetByCodiceAsync(codiceRuolo, cancellationToken);
        if (ruolo is null)
        {
            // Ruolo di sistema mancante: il seed della migration 006 non è stato
            // applicato. Lo segnaliamo come eccezione gestita dal chiamante.
            throw new InvalidOperationException(
                $"Ruolo di sistema '{codiceRuolo}' non trovato: applicare la migration 006.");
        }

        await utenteManager.CreaAsync(
            username: username,
            password: password,
            email: string.IsNullOrWhiteSpace(email) ? null : email,
            idRuolo: ruolo.IdRuolo,
            nomeCompleto: nomeCompleto,
            cancellationToken: cancellationToken);
    }
}
