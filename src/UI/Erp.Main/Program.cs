using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Localization;
using MudBlazor.Services;
using Erp.Main;
using Erp.Main.Endpoints;
using Erp.Main.Services;
using Erp.Common;
using Erp.Common.Configuration;
using Erp.Common.Localization;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information("Starting Erp.Main...");

try
{
var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddInfisicalSecrets(builder.Environment);

builder.Configuration.EnsureConfigured(
    "Services:Api",
    "Services:IdentityApi",
    "OidcConfiguration:Authority",
    "OidcConfiguration:ClientId");

builder.Host.UseSerilog((ctx, lc) => lc
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}")
    .Enrich.FromLogContext()
    .ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddLocalization();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = Constants.Localization.SupportedCultures;

    options.SetDefaultCulture(Constants.Localization.DefaultCulture);
    options.AddSupportedCultures(supportedCultures);
    options.AddSupportedUICultures(supportedCultures);

    // Authenticated users get their language from the "locale" claim issued by Identity; the
    // cookie keeps an anonymous visitor's choice, falling back to the browser's language.
    options.RequestCultureProviders =
    [
        new ClaimsRequestCultureProvider(),
        new CookieRequestCultureProvider { CookieName = Constants.Localization.CultureCookieName },
        new AcceptLanguageHeaderRequestCultureProvider()
    ];
});

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.LoginPath = "/authentication/login";
        options.AccessDeniedPath = "/access-denied";
    })
    .AddOpenIdConnect(options =>
    {
        builder.Configuration.Bind("OidcConfiguration", options);
        options.CallbackPath = "/authentication/login-callback";
        options.SignedOutCallbackPath = "/authentication/logout-callback";
        options.ResponseType = "code";
        options.SaveTokens = true;

        options.Scope.Clear();
        options.Scope.Add(Constants.Scopes.OpenId);
        options.Scope.Add(Constants.Scopes.Profile);
        options.Scope.Add(Constants.Scopes.Email);
        options.Scope.Add(Constants.Scopes.OfflineAccess);
        options.Scope.Add(Constants.Scopes.Roles);
        options.Scope.Add(Constants.Scopes.ErpRead);
        options.Scope.Add(Constants.Scopes.ErpWrite);
        options.Scope.Add(Constants.Scopes.ErpIdentityRead);

        // Duende emits the claim as "role". The default inbound mapping renames it to the long
        // WS-Federation URI, so IsInRole stops matching and an AuthorizeView by role hides
        // itself even from a SuperAdmin. Erp.Api already disables this mapping.
        options.MapInboundClaims = false;
        options.TokenValidationParameters.RoleClaimType = Constants.Claims.Role;
        options.TokenValidationParameters.NameClaimType = Constants.Claims.Name;

        // In code flow the id_token carries no user claims: they live on the userinfo endpoint.
        // Without fetching them the principal has no role at all.
        options.GetClaimsFromUserInfoEndpoint = true;

        // The handler only keeps userinfo claims that a ClaimAction maps, so role needs one of
        // its own. MapJsonKey also expands the array a user with several roles comes back as.
        options.ClaimActions.MapJsonKey(Constants.Claims.Role, Constants.Claims.Role);
    });

// MudBlazor
builder.Services.AddMudServices();

builder.Services.AddTransient<UserAccessTokenHandler>();
builder.Services.AddScoped(sp =>
{
    var navigation = sp.GetRequiredService<NavigationManager>();
    return new HttpClient { BaseAddress = new Uri(navigation.BaseUri) };
});

// The business modules are served by one API; each client keeps its own routes.
var apiBaseAddress = new Uri(builder.Configuration["Services:Api"]!);

builder.Services.AddHttpClient<CoreApiClient>(client => client.BaseAddress = apiBaseAddress)
    .AddHttpMessageHandler<UserAccessTokenHandler>();

builder.Services.AddHttpClient<SalesApiClient>(client => client.BaseAddress = apiBaseAddress)
    .AddHttpMessageHandler<UserAccessTokenHandler>();

builder.Services.AddHttpClient<SeriesApiClient>(client => client.BaseAddress = apiBaseAddress)
    .AddHttpMessageHandler<UserAccessTokenHandler>();

builder.Services.AddHttpClient<SaftApiClient>(client => client.BaseAddress = apiBaseAddress)
    .AddHttpMessageHandler<UserAccessTokenHandler>();

builder.Services.AddHttpClient<CatalogApiClient>(client => client.BaseAddress = apiBaseAddress)
    .AddHttpMessageHandler<UserAccessTokenHandler>();

builder.Services.AddHttpClient<LookupApiClient>(client => client.BaseAddress = apiBaseAddress)
    .AddHttpMessageHandler<UserAccessTokenHandler>();

builder.Services.AddHttpClient<StockApiClient>(client => client.BaseAddress = apiBaseAddress)
    .AddHttpMessageHandler<UserAccessTokenHandler>();

builder.Services.AddHttpClient<PurchasingApiClient>(client => client.BaseAddress = apiBaseAddress)
    .AddHttpMessageHandler<UserAccessTokenHandler>();

builder.Services.AddHttpClient<NotificationApiClient>(client => client.BaseAddress = apiBaseAddress)
    .AddHttpMessageHandler<UserAccessTokenHandler>();

// The Identity host stays separate: it is the token issuer and serves the users API.
builder.Services.AddHttpClient<IdentityApiClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:IdentityApi"]!))
    .AddHttpMessageHandler<UserAccessTokenHandler>();

// A service that stops responding must surface as an error on the page, not as a spinner that
// sits there for the 100 second default of HttpClient.
builder.Services.ConfigureAll<Microsoft.Extensions.Http.HttpClientFactoryOptions>(options =>
    options.HttpClientActions.Add(client => client.Timeout = TimeSpan.FromSeconds(20)));

// Company currently selected in the header, shared by every page of the circuit.
builder.Services.AddScoped<CompanyState>();
builder.Services.AddScoped<WarehouseState>();

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();
app.UseAuthentication();
app.UseRequestLocalization(app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<RequestLocalizationOptions>>().Value);
app.UseAuthorization();

app.MapAuthenticationEndpoints();
app.MapCultureEndpoints();

app.MapRazorComponents<Erp.Main.Shell.App>()
    .AddInteractiveServerRenderMode();

app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Unhandled failure during Erp.Main startup");
}
finally
{
    Log.CloseAndFlush();
}


