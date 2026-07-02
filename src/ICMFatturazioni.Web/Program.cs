using System.Security.Claims;
using ICMFatturazioni.Web.Authentication;
using ICMFatturazioni.Web.Components;
using ICMFatturazioni.Web.Data;
using ICMFatturazioni.Web.Email;
using ICMFatturazioni.Web.Entities;
using ICMFatturazioni.Web.Logging;
using ICMFatturazioni.Web.Manutenzione;
using ICMFatturazioni.Web.Managers;
using ICMFatturazioni.Web.Managers.Interfaces;
using ICMFatturazioni.Web.Repositories;
using ICMFatturazioni.Web.Repositories.Interfaces;
using ICMFatturazioni.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
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
builder.Services.AddScoped<ICodiceIVARepository, CodiceIVARepository>();
builder.Services.AddScoped<IBancaRepository, BancaRepository>();
builder.Services.AddScoped<IAgenziaRepository, AgenziaRepository>();
builder.Services.AddScoped<IBancaAppoggioRepository, BancaAppoggioRepository>();
builder.Services.AddScoped<ITipoPagamentoRepository, TipoPagamentoRepository>();
builder.Services.AddScoped<ICodicePagamentoRepository, CodicePagamentoRepository>();
builder.Services.AddScoped<ITipoAttivitaRepository, TipoAttivitaRepository>();
builder.Services.AddScoped<ITipoDettaglioAttivitaRepository, TipoDettaglioAttivitaRepository>();
builder.Services.AddScoped<IDescrizioneAttivitaRepository, DescrizioneAttivitaRepository>();
builder.Services.AddScoped<IAttivitaRepository, AttivitaRepository>();
builder.Services.AddScoped<IAttivitaDettaglioRepository, AttivitaDettaglioRepository>();
builder.Services.AddScoped<IScadenzaPagamentoRepository, ScadenzaPagamentoRepository>();
builder.Services.AddScoped<ISpesaAnticipataRepository, SpesaAnticipataRepository>();
builder.Services.AddScoped<IAliquotaRepository, AliquotaRepository>();
builder.Services.AddScoped<IAvvisoFatturaRepository, AvvisoFatturaRepository>();
builder.Services.AddScoped<IAziendaRepository, AziendaRepository>();

// LookupRepository singleton: read-only, stateless, dipende solo dalla
// SqlConnectionFactory; alimenta dropdown su più maschere.
builder.Services.AddSingleton<ILookupRepository, LookupRepository>();

// === Manager ===
builder.Services.AddScoped<IUtenteManager, UtenteManager>();
builder.Services.AddScoped<IRuoloManager, RuoloManager>();
builder.Services.AddScoped<IAnagraficaManager, AnagraficaManager>();
builder.Services.AddScoped<ICodiceIVAManager, CodiceIVAManager>();
builder.Services.AddScoped<IBancaManager, BancaManager>();
builder.Services.AddScoped<IAgenziaManager, AgenziaManager>();
builder.Services.AddScoped<IBancaAppoggioManager, BancaAppoggioManager>();
builder.Services.AddScoped<ITipoPagamentoManager, TipoPagamentoManager>();
builder.Services.AddScoped<ICodicePagamentoManager, CodicePagamentoManager>();
builder.Services.AddScoped<ITipoAttivitaManager, TipoAttivitaManager>();
builder.Services.AddScoped<ITipoDettaglioAttivitaManager, TipoDettaglioAttivitaManager>();
builder.Services.AddScoped<IDescrizioneAttivitaManager, DescrizioneAttivitaManager>();
builder.Services.AddScoped<IAttivitaManager, AttivitaManager>();
builder.Services.AddScoped<IAttivitaDettaglioManager, AttivitaDettaglioManager>();
builder.Services.AddScoped<IScadenzaPagamentoManager, ScadenzaPagamentoManager>();
builder.Services.AddScoped<ISpesaAnticipataManager, SpesaAnticipataManager>();
builder.Services.AddScoped<IAliquotaManager, AliquotaManager>();
builder.Services.AddScoped<IAvvisoFatturaManager, AvvisoFatturaManager>();
builder.Services.AddScoped<IAziendaManager, AziendaManager>();

// Servizio puro di calcolo scadenze (stateless) → singleton.
builder.Services.AddSingleton<IScadenzaCalculator, ScadenzaCalculator>();

// Servizio puro di calcolo fiscale dell'avviso (cascata cap. 7) → singleton.
builder.Services.AddSingleton<ICalcoloFiscaleAvviso, CalcoloFiscaleAvviso>();

// Servizio di generazione del PDF dell'avviso di fattura (PDFsharp-MigraDoc).
// Scoped: eredita lo scope dei Manager da cui carica i dati (mirror ICMVerbali).
builder.Services.AddScoped<IAvvisoPdfService, AvvisoPdfService>();

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

// === Diagnostica / logging errori + audit (Regola 6, mirror ICMVerbali) ===
// Pipeline di logging disaccoppiata: il DbLoggerProvider accoda i Warning+ del
// framework (eccezioni non gestite incluse) su una coda in-memory; un
// BackgroundService la drena e scrive a batch su fatt.Log. Repository, coda e
// fallback sono Singleton (vivono fuori dallo scope di richiesta, usati dal
// provider e dal BackgroundService); il LogManager resta Scoped come gli altri.
builder.Services.AddSingleton<ILogRepository, LogRepository>();
builder.Services.AddSingleton<ILogQueue, LogQueue>();
builder.Services.AddSingleton<ILogFallbackWriter, LogFallbackWriter>();
builder.Services.AddScoped<ILogManager, LogManager>();
builder.Services.AddHostedService<LogWriterService>();

// Provider di logging che persiste i Warning+ della pipeline standard. È il modo
// supportato per registrare un provider che ha bisogno di DI. Il filtro forza i
// Warning+ a prescindere dai LogLevel di appsettings.
builder.Services.AddSingleton<ILoggerProvider, DbLoggerProvider>();
builder.Logging.AddFilter<DbLoggerProvider>(null, LogLevel.Warning);

// ProblemDetails per la risposta di errore in produzione (il logging avviene
// ora automaticamente via DbLoggerProvider: niente IExceptionHandler custom).
builder.Services.AddProblemDetails();

// Audit "chi-ha-fatto-cosa" sul CRUD dei dati master. Scoped come gli altri
// Manager/Repository; l'utente corrente è risolto via AuthenticationStateProvider.
builder.Services.AddScoped<IAuditRepository, AuditRepository>();
builder.Services.AddScoped<IAuditManager, AuditManager>();
builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();

// === Retention/manutenzione audit (migration 024 + nota dimensionamento) ===
// Compressione PAGE a DB (migration 024) + retention temporale a 36 mesi e
// sentinella sui 10 GB di Express. La purga è esposta anche manualmente in
// /admin/audit; il job automatico (AuditRetentionService) la applica a cadenza.
builder.Services.Configure<AuditRetentionOptions>(
    builder.Configuration.GetSection(AuditRetentionOptions.SectionName));
builder.Services.AddScoped<IDatabaseSizeRepository, DatabaseSizeRepository>();
builder.Services.AddScoped<IAuditManutenzione, AuditManutenzione>();
builder.Services.AddHostedService<AuditRetentionService>();

var app = builder.Build();

// ============================================================================
// Rete globale di logging (mirror ICMVerbali): eccezioni FUORI dal ciclo di
// richiesta — thread di background, Task non osservate. Quelle dentro le
// richieste HTTP e i circuiti Blazor sono già loggate a livello Error dal
// framework e quindi catturate dal DbLoggerProvider. Qui si accoda (non
// bloccante) sulla stessa coda del provider.
// ============================================================================
var logQueue = app.Services.GetRequiredService<ILogQueue>();
AppDomain.CurrentDomain.UnhandledException += (_, e) =>
{
    if (e.ExceptionObject is Exception ex)
    {
        logQueue.TryEnqueue(CreaLogNonGestito(ex, "AppDomain.UnhandledException",
            "Eccezione non gestita su un thread di background: il processo potrebbe " +
            "terminare. Causa nel dettaglio dell'eccezione."));
    }
};
TaskScheduler.UnobservedTaskException += (_, e) =>
{
    logQueue.TryEnqueue(CreaLogNonGestito(e.Exception, "TaskScheduler.UnobservedTaskException",
        "Eccezione di una Task non osservata (manca un await): potenziale bug di " +
        "concorrenza. Causa nel dettaglio dell'eccezione."));
    e.SetObserved();
};

// ============================================================================
// Pipeline HTTP
// ============================================================================

if (!app.Environment.IsDevelopment())
{
    // In produzione: risposta di errore via ProblemDetails. Il LOGGING delle
    // eccezioni non gestite non è più affidato a un IExceptionHandler custom:
    // il middleware logga l'errore a livello Error e il DbLoggerProvider lo
    // cattura e persiste su fatt.Log automaticamente.
    app.UseExceptionHandler();
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
// In sviluppo resta la developer exception page automatica di WebApplication
// (più ricca per il debug); anch'essa logga a Error → catturata dal provider.
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// L'ordine è vincolante: Authentication deve precedere Authorization,
// ed entrambe devono stare prima di Antiforgery e dei map endpoint.
app.UseAuthentication();
app.UseAuthorization();

// Rate limiter: deve stare dopo l'autorizzazione e prima dei map endpoint.
app.UseRateLimiter();

app.UseAntiforgery();

// AllowAnonymous: senza questo, la fallback policy globale (RequireAuthenticatedUser)
// si applica anche agli asset statici, e una richiesta anonima a /images/*, /css,
// /js verrebbe rediretta a /login. È il motivo per cui il logo nella pagina di
// login (richiesto dal browser prima dell'autenticazione) risultava rotto.
app.MapStaticAssets().AllowAnonymous();
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

// ----------------------------------------------------------------------------
// PDF di cortesia dell'Avviso di fattura (anteprima/stampa).
// Blazor Server non può servire un file binario da un componente: si espone un
// endpoint che la UI apre in una nuova scheda (mirror del pattern PDF di
// ICMVerbali). Servito "inline" così il browser lo mostra come anteprima.
// ----------------------------------------------------------------------------
app.MapGet("/api/avvisi/{id:guid}/pdf", async Task<IResult> (
    Guid id,
    HttpContext httpContext,
    IAvvisoPdfService pdfService,
    IAvvisoFatturaManager avvisoManager,
    ILogManager logManager,
    CancellationToken ct) =>
{
    try
    {
        var pdf = await pdfService.GeneraAsync(id, ct);

        // Nome file leggibile dalla data dell'avviso (best-effort).
        var dettaglio = await avvisoManager.GetDettaglioAsync(id, ct);
        var nomeFile  = dettaglio is not null
            ? $"Avviso_{dettaglio.Testata.DataAvviso:yyyy-MM-dd}.pdf"
            : $"Avviso_{id:D}.pdf";

        httpContext.Response.Headers.ContentDisposition = $"inline; filename=\"{nomeFile}\"";
        return Results.File(pdf, "application/pdf");
    }
    catch (AvvisoPdfNonTrovatoException)
    {
        return Results.NotFound();
    }
    catch (Exception ex)
    {
        await logManager.LogErroreAsync(ex,
            "Generazione del PDF dell'avviso fallita. Cause tipiche: font di sistema non " +
            "risolto sull'host, dati azienda/cliente incoerenti.",
            "AvvisoPdf.GeneraAsync", entityId: id, entityType: "AvvisoFattura", cancellationToken: ct);
        return Results.Problem("Errore durante la generazione del PDF dell'avviso.", statusCode: 500);
    }
})
.RequireAuthorization();

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

// Costruisce la riga di log per un'eccezione non gestita fuori richiesta
// (rete globale). Livello Critical: è il tipo di errore che può abbattere il
// processo. Messaggio e stack sanitizzati come ovunque (Regola 6).
static ICMFatturazioni.Web.Entities.Log CreaLogNonGestito(Exception ex, string sorgente, string spiegazione) => new()
{
    Id = Guid.CreateVersion7(),
    TimestampUtc = DateTime.UtcNow,
    Livello = ICMFatturazioni.Web.Entities.LogLivello.Critical,
    Sorgente = sorgente,
    Messaggio = LogSanitizer.Sanitize(ex.Message) ?? string.Empty,
    EccezioneTipo = ex.GetType().FullName,
    StackTrace = LogSanitizer.Sanitize(ex.ToString()),
    SpiegazioneUtente = spiegazione,
};
