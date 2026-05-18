using ICMFatturazioni.Web.Components;
using ICMFatturazioni.Web.Data;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// === Blazor Web App – Interactive Server ===
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// === Authentication (cookie) ===
// Schema cookie semplice. Dettagli specifici (claim transformer,
// gestione lockout, refresh sliding personalizzato) verranno
// aggiunti in Authentication/CookieAuthHandler.cs nelle iterazioni
// successive. Qui resta solo il cablaggio minimo richiesto perché
// gli endpoint Blazor possano leggere l'identità autenticata.
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/access-denied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

// Propaga AuthenticationState ai componenti Blazor tramite cascading
// parameter: serve perché gli @attribute [Authorize] e i componenti
// AuthorizeView funzionino senza inject manuale ad ogni livello.
builder.Services.AddCascadingAuthenticationState();

// === Data access ===
// Singleton: la factory è stateless, costruisce SqlConnection nuove
// a ogni richiesta. Non viene mai condivisa una connessione fra
// scope diversi, quindi non c'è rischio di thread-safety.
builder.Services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
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

app.Run();
