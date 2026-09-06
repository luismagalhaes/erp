using Erp.Identity.Application;
using Erp.Identity.Endpoints;
using Erp.Identity.Data;
using Erp.Identity.Dependencies;
using Erp.Identity.Storage;
using Duende.IdentityServer;
using Microsoft.AspNetCore.Identity;
using MudBlazor.Services;
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
    builder.Services.AddIdentityDependencies(builder.Configuration);
    builder.Services.AddMudServices();

    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();
    builder.Services.AddCascadingAuthenticationState();
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
    app.UseIdentityServer();
    app.UseAuthorization();
    app.UseAntiforgery();

    app.MapAuthenticationEndpoints();

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

