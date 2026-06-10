using System.Security.Claims;
using ICMFatturazioni.Web.Authentication;
using ICMFatturazioni.Web.Components;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Diagnostics;
using ICMFatturazioni.Web.Email;
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
using Microsoft.Extensions.Options;
using MudBlazor.Services;
using System.Threading.RateLimiting;

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
builder.Services.AddScoped<IMenuRepository, MenuRepository>();
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

// === Menu dinamico / autorizzazione per ruolo ===
// MenuService scoped: calcola una volta per circuit l'albero visibile e le
// pagine consentite all'utente. PageRouteResolver singleton: mappa
// nome-classe → rotta una sola volta (riflessione all'avvio).
builder.Services.AddScoped<IMenuService, MenuService>();
builder.Services.AddScoped<IMenuConfigManager, MenuConfigManager>();
builder.Services.AddSingleton<ICMFatturazioni.Web.Navigation.IPageRouteResolver,
    ICMFatturazioni.Web.Navigation.PageRouteResolver>();

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

// === Magic-link utente (attivazione/reset password, T4) + invio email ===
// TimeProvider di sistema: iniettato nel UtenteTokenManager per calcolare le
// scadenze; nei test si sostituisce con un provider pilotabile.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<UtenteTokenOptions>(
    builder.Configuration.GetSection(UtenteTokenOptions.SectionName));
builder.Services.Configure<SmtpOptions>(
    builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.AddScoped<IUtenteTokenRepository, UtenteTokenRepository>();
builder.Services.AddScoped<IUtenteTokenManager, UtenteTokenManager>();

// IEmailSender: SMTP reale (MailKit) se "Smtp:Host" è configurato; altrimenti
// il LogEmailSender di sviluppo (il link finisce nel log, nessun invio reale).
var smtpOptions = builder.Configuration.GetSection(SmtpOptions.SectionName).Get<SmtpOptions>();
if (smtpOptions?.IsConfigured == true)
{
    builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
}
else
{
    builder.Services.AddScoped<IEmailSender, LogEmailSender>();
}

// Rate limiting: max 5 richieste/min per IP sul "password dimenticata", per non
// trasformarlo in un canale di spam email o in un oracolo di esistenza account.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("forgot-password", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
            }));
});

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

// Rate limiter: deve stare dopo l'autorizzazione e prima dei map endpoint.
app.UseRateLimiter();

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

    // Firma il cookie con i claim dell'utente (logica condivisa con i flussi di
    // attivazione/reset, che fanno sign-in automatico dopo il set-password).
    await SignInUtenteAsync(httpContext, utente, ruoloManager, cancellationToken);

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

// ----------------------------------------------------------------------------
// Magic-link: imposta password da link di ATTIVAZIONE (primo accesso).
// Il consumo del token e l'impostazione della password sono atomici (sentinel
// TOCTOU nel repository). A successo, sign-in automatico e redirect alla home.
// ----------------------------------------------------------------------------
app.MapPost("/auth/attiva", async Task<IResult> (
    [FromForm] string Token,
    [FromForm] string Password,
    [FromForm] string PasswordConfirm,
    HttpContext httpContext,
    IUtenteTokenManager tokenManager,
    IUtenteManager utenteManager,
    IRuoloManager ruoloManager,
    IPasswordHasherService hasher,
    CancellationToken cancellationToken) =>
{
    var erroreInput = ValidaNuovaPassword(Password, PasswordConfirm);
    if (erroreInput is not null)
    {
        return Results.Redirect(BuildTokenRedirect("/attiva", Token, erroreInput));
    }

    try
    {
        var hash = hasher.HashPassword(Password);
        var utenteId = await tokenManager.ConsumaAsync(Token, UtenteTokenTipo.Attivazione, hash, cancellationToken);
        var utente = await utenteManager.GetByIdAsync(utenteId, cancellationToken);
        if (utente is null)
        {
            return Results.Redirect(BuildTokenRedirect("/attiva", Token, "Utente non trovato."));
        }
        await SignInUtenteAsync(httpContext, utente, ruoloManager, cancellationToken);
        return Results.Redirect("/");
    }
    catch (UtenteTokenInvalidoException ex)
    {
        return Results.Redirect(BuildTokenRedirect("/attiva", Token, UtenteTokenMessaggi.Messaggio(ex.Motivo)));
    }
})
.AllowAnonymous();

// ----------------------------------------------------------------------------
// Magic-link: imposta nuova password da link di RESET. Identico ad /auth/attiva
// ma con token di tipo Reset.
// ----------------------------------------------------------------------------
app.MapPost("/auth/reset-password", async Task<IResult> (
    [FromForm] string Token,
    [FromForm] string Password,
    [FromForm] string PasswordConfirm,
    HttpContext httpContext,
    IUtenteTokenManager tokenManager,
    IUtenteManager utenteManager,
    IRuoloManager ruoloManager,
    IPasswordHasherService hasher,
    CancellationToken cancellationToken) =>
{
    var erroreInput = ValidaNuovaPassword(Password, PasswordConfirm);
    if (erroreInput is not null)
    {
        return Results.Redirect(BuildTokenRedirect("/reset-password", Token, erroreInput));
    }

    try
    {
        var hash = hasher.HashPassword(Password);
        var utenteId = await tokenManager.ConsumaAsync(Token, UtenteTokenTipo.Reset, hash, cancellationToken);
        var utente = await utenteManager.GetByIdAsync(utenteId, cancellationToken);
        if (utente is null)
        {
            return Results.Redirect(BuildTokenRedirect("/reset-password", Token, "Utente non trovato."));
        }
        await SignInUtenteAsync(httpContext, utente, ruoloManager, cancellationToken);
        return Results.Redirect("/");
    }
    catch (UtenteTokenInvalidoException ex)
    {
        return Results.Redirect(BuildTokenRedirect("/reset-password", Token, UtenteTokenMessaggi.Messaggio(ex.Motivo)));
    }
})
.AllowAnonymous();

// ----------------------------------------------------------------------------
// "Password dimenticata": l'utente inserisce l'email; se corrisponde a un
// account ATTIVO e già attivato, gli inviamo un link di reset. La risposta è
// SEMPRE neutra (niente enumeration). Rate-limited per IP.
// ----------------------------------------------------------------------------
app.MapPost("/auth/forgot-password", async Task<IResult> (
    [FromForm] string Email,
    HttpContext httpContext,
    IUtenteManager utenteManager,
    IUtenteTokenManager tokenManager,
    IEmailSender emailSender,
    IOptions<UtenteTokenOptions> tokenOptions,
    CancellationToken cancellationToken) =>
{
    if (!string.IsNullOrWhiteSpace(Email))
    {
        var utente = await utenteManager.GetByEmailAsync(Email, cancellationToken);
        // Solo utenti attivi, con email e già attivati (hanno una password):
        // un invitato non ancora attivato usa il link di attivazione, non il reset.
        if (utente is { Attivo: true, Email: not null } && !string.IsNullOrEmpty(utente.PasswordHash))
        {
            var token = await tokenManager.CreaResetAsync(utente.IdUtente, cancellationToken);
            var link = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/reset-password/{token}";
            var (subject, htmlBody) = AccountEmailTemplates.Reset(link, utente.Username, tokenOptions.Value.ResetOreDefault);
            await emailSender.SendAsync(utente.Email, subject, htmlBody, cancellationToken);
        }
    }
    // Esito neutro a prescindere: non riveliamo se l'email esiste.
    return Results.Redirect("/forgot-password?inviato=1");
})
.AllowAnonymous()
.RequireRateLimiting("forgot-password");

// Il seed degli utenti Admin/Superadmin è gestito da DatabaseSeeder
// (IHostedService idempotente, registrato sopra): legge le password da
// user-secrets/env e crea gli utenti all'avvio se non esistono.

app.Run();

// ============================================================================
// Helper locali condivisi dagli endpoint di autenticazione
// ============================================================================

// Firma il cookie con i claim dell'utente. Centralizzato: lo usano login,
// attivazione e reset (questi ultimi due con sign-in automatico). Il claim di
// ruolo porta il Codice del ruolo (SUPERADMIN/ADMIN) così le policy RequireRole
// combaciano; per i custom (Codice null) si usa il nome. id_ruolo serve al
// servizio menu (T2) per i permessi dinamici.
static async Task SignInUtenteAsync(HttpContext httpContext, Utente utente, IRuoloManager ruoloManager, CancellationToken cancellationToken)
{
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
    await httpContext.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identity));
}

// Validazione server-side della nuova password: policy condivisa + conferma.
static string? ValidaNuovaPassword(string? password, string? confirm)
{
    var errore = PasswordPolicy.Valida(password);
    if (errore is not null)
    {
        return errore;
    }
    return password != confirm ? "Le due password non coincidono." : null;
}

// Redirect alla pagina del magic-link con il token nella rotta e il messaggio
// d'errore in querystring (la pagina lo mostra sopra il form).
static string BuildTokenRedirect(string basePath, string token, string errore)
    => $"{basePath}/{Uri.EscapeDataString(token)}?Error={Uri.EscapeDataString(errore)}";
