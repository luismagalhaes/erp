using Erp.Identity.Application;
using Erp.Identity.Common.Constants;
using Erp.Identity.Endpoints;
using Erp.Identity.Data;
using Erp.Identity.Dependencies;
using Erp.Identity.Storage;
using Erp.Identity.Common.Configuration;
using Erp.Identity.Telemetry;
using Duende.IdentityServer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using MudBlazor.Services;
using Scalar.AspNetCore;
using Erp.Identity.Common.Localization;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddInfisicalSecrets(builder.Environment);

builder.Configuration.EnsureConfigured(
    "ConnectionStrings:IdentityDb",
    "IdentityServer:Authority",
    "AdminUser:Email",
    "AdminUser:Password",
    "ErpApi:BaseUrl",
    "ErpIdentityClient:Authority",
    "ErpIdentityClient:ClientId",
    "ErpIdentityClient:ClientSecret");

builder.AddErpOpenTelemetry(Constants.ApiResources.IdentityApi);

builder.Services.AddIdentityStorage(builder.Configuration);
builder.Services.AddIdentityApplication();
// Outside development the service secret has to come from user secrets or a secret store.
builder.Services.AddIdentityDependencies(
    builder.Configuration,
    builder.Environment.IsDevelopment() ? Constants.Clients.ErpIdentityDevelopmentSecret : null);
builder.Services.AddMudServices();
// SignIn.razor/SignUp.razor read the caller's IP during their initial (pre-interactive) render to
// decide whether reCAPTCHA is required yet — only valid at that point, never inside the circuit.
builder.Services.AddHttpContextAccessor();

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
builder.Services.AddOpenApi();

// Bearer scheme for the users API this host serves. The default schemes stay the cookie
// ones set up by ASP.NET Identity, so the UI is unaffected.
var authenticationBuilder = builder.Services.AddAuthentication()
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

// Service to service only: the ERP API raises and reads onboarding requests with a client
// credentials token carrying this scope, which no user facing client is ever granted.
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Constants.Policies.Onboarding, policy => policy.RequireAssertion(context =>
        context.User.FindAll("scope")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(Constants.Scopes.ErpIdentityOnboarding, StringComparer.Ordinal)));

var googleClientId =builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];

// Optional, not EnsureConfigured: an environment with no Google credentials set still starts
// fine, it just doesn't offer the "continue with Google" button (SignIn.razor hides it too).
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    authenticationBuilder.AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.Scope.Add("email");
    });
}

var microsoftClientId = builder.Configuration["Authentication:Microsoft:ClientId"];
var microsoftClientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"];

// Same optional pattern as Google above: no credentials configured just means no Microsoft
// button and no handler registered, not a startup failure.
if (!string.IsNullOrWhiteSpace(microsoftClientId) && !string.IsNullOrWhiteSpace(microsoftClientSecret))
{
    authenticationBuilder.AddMicrosoftAccount(options =>
    {
        options.ClientId = microsoftClientId;
        options.ClientSecret = microsoftClientSecret;

        // Microsoft Graph's "mail" field — the default claim source for ClaimTypes.Email — is
        // null for a lot of real accounts (personal Microsoft accounts especially, but also some
        // work/school ones without an Exchange mailbox). "userPrincipalName" is always populated
        // and is an email address in the vast majority of tenants, so it's kept as a fallback
        // claim for HandleExternalLoginCallbackAsync to read when "mail" comes back empty.
        options.ClaimActions.MapJsonKey(AuthenticationEndpoints.MicrosoftUserPrincipalNameClaimType, "userPrincipalName");
    });
}

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

// Unlike the ERP API, "/" stays the Blazor UI's home page: Scalar is only reached by typing /scalar.
app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.Title = "ERP Identity API";
});

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
