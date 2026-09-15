using Erp.Identity.Application;
using Erp.Identity.Common.Constants;
using Erp.Identity.Endpoints;
using Erp.Identity.Data;
using Erp.Identity.Dependencies;
using Erp.Identity.Storage;
using Erp.Identity.Common.Configuration;
using Erp.Identity.Telemetry;
using Duende.IdentityServer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using MudBlazor.Services;
using Erp.Identity.Common.Localization;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddInfisicalSecrets(builder.Environment);

builder.Configuration.EnsureConfigured(
    "ConnectionStrings:IdentityDb",
    "IdentityServer:Authority",
    "AdminUser:Email",
    "AdminUser:Password",
    "NotificationService:BaseUrl",
    "ServiceAuthentication:Authority",
    "ServiceAuthentication:ClientId",
    "ServiceAuthentication:ClientSecret");

builder.AddErpOpenTelemetry(Constants.ApiResources.IdentityApi);

builder.Services.AddIdentityStorage(builder.Configuration);
builder.Services.AddIdentityApplication();
// Outside development the service secret has to come from user secrets or a secret store.
builder.Services.AddIdentityDependencies(
    builder.Configuration,
    builder.Environment.IsDevelopment() ? Constants.Clients.IdentityServiceSecret : null);
builder.Services.AddMudServices();

builder.Services.AddLocalization();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = Constants.Localization.SupportedCultures;

    options.SetDefaultCulture(Constants.Localization.DefaultCulture);
    options.AddSupportedCultures(supportedCultures);
    options.AddSupportedUICultures(supportedCultures);

    // Authenticated users get their language from the "locale" claim, so a signed-in
    // account keeps its preferred language even from a different browser/device.
    options.RequestCultureProviders =
    [
        new ClaimsRequestCultureProvider(),
        new CookieRequestCultureProvider { CookieName = Constants.Localization.CultureCookieName },
        new AcceptLanguageHeaderRequestCultureProvider()
    ];
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
// The logging filter runs once per action instead of every action logging its own entry — see
// RequestLoggingFilter.
builder.Services.AddControllers(options => options.Filters.Add<RequestLoggingFilter>());

// Bearer scheme for the users API this host serves. The default schemes stay the cookie
// ones set up by ASP.NET Identity, so the UI is unaffected.
builder.Services.AddAuthentication()
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["IdentityServer:Authority"];
        options.Audience = Constants.ApiResources.IdentityApi;
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

        // JwtBearerOptions.MapInboundClaims defaults to true, which renames 'role' to the
        // WS-Federation URI. Keeping the short names is what makes the role checks find the
        // claim Duende actually issued.
        options.MapInboundClaims = false;
        options.TokenValidationParameters.RoleClaimType = Constants.Claims.Role;
        options.TokenValidationParameters.NameClaimType = Constants.Claims.Name;
    });
builder.Services.AddCors(options =>
    options.AddPolicy("BlazorPolicy", policy =>
        policy.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [])
              .AllowAnyHeader()
              .AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();

app.UseStaticFiles();
app.UseRouting();
app.UseCors("BlazorPolicy");
app.UseAuthentication();
app.UseRequestLocalization(app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<RequestLocalizationOptions>>().Value);
app.UseIdentityServer();
app.UseAuthorization();
app.UseAntiforgery();

app.MapAuthenticationEndpoints();
app.MapCultureEndpoints();
app.MapControllers();

app.MapRazorComponents<Erp.Identity.Shell.App>()
    .AddInteractiveServerRenderMode();
// Both migrations and seeding run on every startup: migrations are never destructive, and the
// seed (clients, scopes, resources, roles, admin user) is critical for the app to function.
// The seed *overwrites* what's configured in code, so backoffice changes are lost on restart —
// that's a known limitation. If you need to preserve those changes, you'd need to store
// configuration in the database instead of in code.
await SeedData.MigrateAsync(app.Services);
await SeedData.InitializeAsync(app.Services);

await app.RunAsync();
