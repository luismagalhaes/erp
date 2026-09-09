using Erp.Identity.Application;
using Erp.Identity.Common.Constants;
using Erp.Identity.Endpoints;
using Erp.Identity.Data;
using Erp.Identity.Dependencies;
using Erp.Identity.Storage;
using Duende.IdentityServer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using MudBlazor.Services;
using Erp.Common.Localization;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information("Starting Erp.Identity...");

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, lc) => lc
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}")
        .Enrich.FromLogContext()
        .ReadFrom.Configuration(ctx.Configuration));

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
        var supportedCultures = Erp.Common.Constants.Localization.SupportedCultures;

        options.SetDefaultCulture(Erp.Common.Constants.Localization.DefaultCulture);
        options.AddSupportedCultures(supportedCultures);
        options.AddSupportedUICultures(supportedCultures);

        // Authenticated users get their language from the "locale" claim, so a signed-in
        // account keeps its preferred language even from a different browser/device.
        options.RequestCultureProviders =
        [
            new ClaimsRequestCultureProvider(),
            new CookieRequestCultureProvider { CookieName = Erp.Common.Constants.Localization.CultureCookieName },
            new AcceptLanguageHeaderRequestCultureProvider()
        ];
    });

    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();
    builder.Services.AddCascadingAuthenticationState();
    builder.Services.AddControllers();

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

    app.UseSerilogRequestLogging();
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
    if (args.Contains("--seed") || app.Environment.IsDevelopment())
        await SeedData.InitializeAsync(app.Services);

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Unhandled failure during Identity Server startup");
}
finally
{
    await Log.CloseAndFlushAsync();
}

