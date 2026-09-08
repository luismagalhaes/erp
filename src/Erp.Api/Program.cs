using Erp.Api.Authorization;
using Erp.Core.Application;
using Erp.Core.Storage;
using Erp.Common;
using Erp.FiscalPT;
using Erp.FiscalPT.Saft;
using Erp.Notification.Application;
using Erp.Notification.Storage;
using Erp.Inventory.Application;
using Erp.Inventory.Storage;
using Erp.Purchasing.Application;
using Erp.Purchasing.Storage;
using Erp.Sales.Application;
using Erp.Sales.Storage;
using Erp.SeriesRegistry;
using Erp.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();
Log.Information("Starting Erp.Api...");

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, lc) => lc
        .WriteTo.Console()
        .Enrich.FromLogContext()
        .ReadFrom.Configuration(ctx.Configuration));

    // One context for every business module: they share a database, so sharing the context is what
    // lets a document, the stock it moves and the order it came from be written in one transaction.
    // Each module then adds its own tables to that model, and its services.
    builder.Services.AddErpStorage(builder.Configuration);

    builder.Services.AddCoreStorage();
    builder.Services.AddCoreApplication();

    // The series registry comes before the modules that take numbers from it.
    builder.Services.AddSeries();

    // What every module that issues fiscal documents shares: the signing key, the signer, and the
    // SAF-T exporter, which collects from whichever modules register an ISaftDocumentSource below.
    builder.Services.AddFiscalPT(
        builder.Configuration,
        allowDevelopmentKeyGeneration: builder.Environment.IsDevelopment());

    builder.Services.AddSalesStorage();
    builder.Services.AddSalesApplication();

    builder.Services.AddInventoryStorage();
    builder.Services.AddInventoryApplication();

    builder.Services.AddPurchasingStorage();
    builder.Services.AddPurchasingApplication();

    builder.Services.AddNotificationStorage();
    builder.Services.AddNotificationApplication(builder.Configuration);

    builder.Services.AddControllers();

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = builder.Configuration["IdentityServer:Authority"];
            options.Audience = Constants.ApiResources.ErpApi;
            options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

            // JwtBearerOptions.MapInboundClaims defaults to true, which renames 'role' to the
            // WS-Federation URI. Keeping the short names is what makes the role checks find the
            // claim Duende actually issued.
            options.MapInboundClaims = false;
            options.TokenValidationParameters.RoleClaimType = Constants.Claims.Role;
            options.TokenValidationParameters.NameClaimType = Constants.Claims.Name;
        });

    builder.Services.AddAuthorizationBuilder()
        .AddErpPolicies();

    builder.Services.AddOpenApi();

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options.Title = "ERP API";
        });

        app.MapGet("/", () => Results.Redirect("/scalar"));
    }

    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Failure during Erp.Api startup");
}
finally
{
    Log.CloseAndFlush();
}
