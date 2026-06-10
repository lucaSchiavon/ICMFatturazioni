using System.Security.Claims;
using ICMFatturazioni.Web.Authentication;
using ICMFatturazioni.Web.Components;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Diagnostics;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Managers;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Repositories;
using ICMFatturazioni.Web.Repositories.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Mvc;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// === Blazor Web App – Interactive Server ===
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// === MudBlazor (ADR D10–D18) ===
// AddMudServices registra in un colpo solo Snackbar, DialogService,
// ResizeListener, BreakpointService, ecc. La tematizzazione concreta è
// applicata in MainLayout via <MudThemeProvider Theme="@IcmTheme.Default" />.
builder.Services.AddMudServices();

// === Authentication (cookie) ===
// Schema cookie semplice. Dettagli specifici (claim transformer,
// gestione lockout, refresh sliding personalizzato) verranno
// aggiunti nelle iterazioni successive. Qui resta solo il cablaggio
// minimo richiesto perché gli endpoint Blazor possano leggere
// l'identità autenticata.
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/access-denied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

// Fallback policy: richiede autenticazione su tutto l'app. Le pagine
// pubbliche (/login, /access-denied) devono avere [AllowAnonymous].
// Policy inclusive sui ruoli di sistema (riconosciuti dal claim di ruolo =
// Codice del ruolo): RequireAdmin vale per Admin O Superadmin; RequireSuperadmin
// solo per Superadmin (es. pagina log errori). I ruoli custom NON passano da
// qui: la loro visibilità è guidata dal mapping dinamico ruolo↔menu (T2).
builder.Services
    .AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build())
    .AddPolicy(AuthorizationPolicies.RequireAdmin, policy =>
        policy.RequireRole(RuoliSistema.Admin, RuoliSistema.Superadmin))
    .AddPolicy(AuthorizationPolicies.RequireSuperadmin, policy =>
        policy.RequireRole(RuoliSistema.Superadmin));

// Propaga AuthenticationState ai componenti Blazor tramite cascading
// parameter: serve perché gli @attribute [Authorize] e i componenti
// AuthorizeView funzionino senza inject manuale ad ogni livello.
builder.Services.AddCascadingAuthenticationState();

// HttpContextAccessor: necessario all'ErrorLogger per estrarre user e
// request path al momento del log.
builder.Services.AddHttpContextAccessor();

// === Data access ===
// Singleton: la factory è stateless, costruisce SqlConnection nuove
// a ogni richiesta. Non viene mai condivisa una connessione fra
// scope diversi, quindi non c'è rischio di thread-safety.
builder.Services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();

// === Repository ===
// Scoped per i repository di dominio: una istanza per request HTTP /
// circuit Blazor, coerente con la durata della connessione SQL.
builder.Services.AddScoped<IUtenteRepository, UtenteRepository>();
builder.Services.AddScoped<IRuoloRepository, RuoloRepository>();
builder.Services.AddScoped<IAnagraficaRepository, AnagraficaRepository>();

// LookupRepository singleton: read-only, stateless, dipende solo dalla
// SqlConnectionFactory; alimenta dropdown su più maschere.
builder.Services.AddSingleton<ILookupRepository, LookupRepository>();

// ErrorLogRepository singleton: non ha stato, dipende solo dalla
// ISqlConnectionFactory (anch'essa singleton) e crea una connessione
// nuova ad ogni Insert. Deve essere singleton perché viene iniettato
// in IErrorLogger, che è singleton (e Microsoft.Extensions.DI vieta a
// un singleton di consumare uno scoped).
builder.Services.AddSingleton<IErrorLogRepository, ErrorLogRepository>();

// === Manager ===
builder.Services.AddScoped<IUtenteManager, UtenteManager>();
builder.Services.AddScoped<IRuoloManager, RuoloManager>();
builder.Services.AddScoped<IAnagraficaManager, AnagraficaManager>();

// === Hashing password + seed utenti ===
// PasswordHasherService singleton: stateless, PBKDF2 via il framework.
builder.Services.AddSingleton<IPasswordHasherService, PasswordHasherService>();
// Opzioni di seed (password da user-secrets in dev / env var in prod).
builder.Services.Configure<AdminSeederOptions>(
    builder.Configuration.GetSection(AdminSeederOptions.SectionName));
builder.Services.Configure<SuperadminSeederOptions>(
    builder.Configuration.GetSection(SuperadminSeederOptions.SectionName));
// Seed idempotente di Admin/Superadmin all'avvio (sostituisce il vecchio
// seed hardcoded admin/admin1234).
builder.Services.AddHostedService<DatabaseSeeder>();

// === Diagnostica / logging errori (Regola 6) ===
// IErrorLogger singleton: è il canale "infallibile" usato da middleware,
// circuit handler, ErrorBoundary e dai Manager.
builder.Services.AddSingleton<IErrorLogger, ErrorLogger>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// CircuitHandler scoped: una istanza per circuit Blazor. Si occupa di
// sottoscrivere UnobservedTaskException la prima volta che viene aperto
// un circuit.
builder.Services.AddScoped<CircuitHandler, IcmCircuitHandler>();

var app = builder.Build();

// ============================================================================
// Pipeline HTTP
// ============================================================================

// L'exception handler globale richiede UseExceptionHandler ANCHE in
// development per attivare la pipeline e invocare GlobalExceptionHandler.
// In dev la dev page rimane: GlobalExceptionHandler ritorna false e
// passa la palla alla pagina di errore di default.
app.UseExceptionHandler();

if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// L'ordine è vincolante: Authentication deve precedere Authorization,
// ed entrambe devono stare prima di Antiforgery e dei map endpoint.
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// ============================================================================
// Endpoint di autenticazione (minimal API)
// ----------------------------------------------------------------------------
// Blazor Server interattivo non può chiamare SignInAsync/SignOutAsync da
// un componente perché la response è già committata al circuit. Per
// questo il form di /login POSTa qui, dove HttpContext è ancora "fresco".
// ============================================================================

app.MapPost("/auth/login", async Task<IResult> (
    [FromForm] string Username,
    [FromForm] string Password,
    [FromForm] string? ReturnUrl,
    HttpContext httpContext,
    IUtenteManager utenteManager,
    IRuoloManager ruoloManager,
    CancellationToken cancellationToken) =>
{
    var utente = await utenteManager.AutenticaAsync(Username, Password, cancellationToken);
    if (utente is null)
    {
        // Messaggio volutamente generico per non distinguere "username
        // sconosciuto" da "password errata" (anti-enumeration).
        return Results.Redirect("/login?Error=Credenziali non valide.");
    }

    // Ruolo dell'utente: il claim di ruolo porta il Codice del ruolo
    // (SUPERADMIN/ADMIN per i fissi) così le policy RequireRole combaciano;
    // per i ruoli custom (Codice null) usiamo il nome. id_ruolo serve al
    // servizio menu (T2) per risolvere i permessi dinamici.
    var ruolo = await ruoloManager.GetByIdAsync(utente.IdRuolo, cancellationToken);

    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, utente.IdUtente.ToString()),
        new(ClaimTypes.Name, utente.Username),
        new(ClaimTypes.Role, ruolo?.Codice ?? ruolo?.Nome ?? string.Empty),
        new("id_ruolo", utente.IdRuolo.ToString()),
        new("ruolo_codice", ruolo?.Codice ?? string.Empty),
    };
    if (!string.IsNullOrWhiteSpace(utente.NomeCompleto))
    {
        claims.Add(new Claim("nome_completo", utente.NomeCompleto));
    }
    claims.Add(new Claim("tema_preferito", utente.TemaPreferito));

    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    var principal = new ClaimsPrincipal(identity);

    await httpContext.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        principal);

    // Protezione open-redirect: accettiamo solo redirect a path locali.
    var safeReturn = (!string.IsNullOrEmpty(ReturnUrl) && Uri.IsWellFormedUriString(ReturnUrl, UriKind.Relative))
        ? ReturnUrl
        : "/";
    return Results.Redirect(safeReturn);
})
.AllowAnonymous();

app.MapPost("/auth/logout", async (HttpContext httpContext) =>
{
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
});

// Il seed degli utenti Admin/Superadmin è gestito da DatabaseSeeder
// (IHostedService idempotente, registrato sopra): legge le password da
// user-secrets/env e crea gli utenti all'avvio se non esistono.

app.Run();
