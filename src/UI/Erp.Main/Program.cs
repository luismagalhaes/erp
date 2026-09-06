using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components;
using MudBlazor.Services;
using Erp.Main;
using Erp.Main.Endpoints;
using Erp.Main.Services;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information("Starting Erp.Main...");

try
{
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}")
    .Enrich.FromLogContext()
    .ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Scope.Add("offline_access");
        options.Scope.Add("erp.core.read");
        options.Scope.Add("erp.core.write");
        options.Scope.Add("erp.sales.read");
        options.Scope.Add("erp.sales.write");
        options.Scope.Add("erp.inventory.read");
        options.Scope.Add("erp.inventory.write");
        options.Scope.Add("erp.purchasing.read");
        options.Scope.Add("erp.purchasing.write");
        options.Scope.Add("erp.accounting.read");
        options.Scope.Add("erp.accounting.write");
        options.Scope.Add("erp.reporting.read");
    });

// MudBlazor
builder.Services.AddMudServices();

builder.Services.AddTransient<UserAccessTokenHandler>();
builder.Services.AddScoped(sp =>
{
    var navigation = sp.GetRequiredService<NavigationManager>();
    return new HttpClient { BaseAddress = new Uri(navigation.BaseUri) };
});

// Typed HttpClients per microservice
builder.Services.AddHttpClient<CoreApiClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:CoreApi"]!))
    .AddHttpMessageHandler<UserAccessTokenHandler>();

builder.Services.AddHttpClient<SalesApiClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:SalesApi"]!))
    .AddHttpMessageHandler<UserAccessTokenHandler>();

builder.Services.AddHttpClient<InventoryApiClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:InventoryApi"]!))
    .AddHttpMessageHandler<UserAccessTokenHandler>();

builder.Services.AddHttpClient<PurchasingApiClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:PurchasingApi"]!))
    .AddHttpMessageHandler<UserAccessTokenHandler>();

builder.Services.AddHttpClient<AccountingApiClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:AccountingApi"]!))
    .AddHttpMessageHandler<UserAccessTokenHandler>();

builder.Services.AddHttpClient<ReportingApiClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:ReportingApi"]!))
    .AddHttpMessageHandler<UserAccessTokenHandler>();

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthenticationEndpoints();

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


