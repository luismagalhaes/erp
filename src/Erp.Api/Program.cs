using Erp.Api.Authorization;
using Erp.Core.Application;
using Erp.Core.Storage;
using Erp.Common;
using Erp.Notification.Application;
using Erp.Notification.Storage;
using Erp.Inventory.Application;
using Erp.Inventory.Storage;
using Erp.Purchasing.Application;
using Erp.Purchasing.Storage;
using Erp.Sales.Application;
using Erp.Sales.Storage;
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

    // One host, one module per folder. Each module keeps its own layers, its own schema and its
    // own database; only the process is shared.
    builder.Services.AddCoreStorage(builder.Configuration);
    builder.Services.AddCoreApplication();

    builder.Services.AddSalesStorage(builder.Configuration);
    builder.Services.AddSalesApplication(
        builder.Configuration,
        allowDevelopmentKeyGeneration: builder.Environment.IsDevelopment());

    builder.Services.AddInventoryStorage(builder.Configuration);
    builder.Services.AddInventoryApplication();

    builder.Services.AddPurchasingStorage(builder.Configuration);
    builder.Services.AddPurchasingApplication();

    builder.Services.AddNotificationStorage(builder.Configuration);
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
