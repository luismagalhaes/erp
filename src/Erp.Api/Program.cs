using Erp.Api.Services;
using Erp.Common;
using Erp.Common.Configuration;
using Erp.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OData;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();
Log.Information("Starting Erp.Api...");

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Configuration.AddInfisicalSecrets(builder.Environment);

    var requiredSettings = new List<string> { "ConnectionStrings:ErpDb", "IdentityServer:Authority" };
    if (!builder.Environment.IsDevelopment())
        requiredSettings.Add("Fiscal:PrivateKeyPem");
    builder.Configuration.EnsureConfigured([.. requiredSettings]);

    builder.Host.UseSerilog((ctx, lc) => lc
        .WriteTo.Console()
        .Enrich.FromLogContext()
        .ReadFrom.Configuration(ctx.Configuration));

    // Every business module, in one call, so the integration tests can build the same container
    // instead of a lookalike of their own.
    builder.Services.AddModules(
        builder.Configuration,
        allowDevelopmentKeyGeneration: builder.Environment.IsDevelopment());

    // OData is used only as a query language over the existing REST routes: it lets the data grids
    // push filtering, sorting and paging down to SQL instead of loading everything into the client.
    builder.Services.AddControllers()
        .AddOData(options => options
            .Select()
            .Filter()
            .OrderBy()
            .Count()
            .SetMaxTop(500));

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
        .AddPolicies();

    builder.Services.AddOpenApi();

    var app = builder.Build();

    // Applying pending migrations is never destructive, so it runs on every startup, in every
    // environment — the deploy pipeline only ships code, nothing there ever touches the schema.
    await using (var scope = app.Services.CreateAsyncScope())
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();

    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "ERP API";
    });

    app.MapGet("/", () => Results.Redirect("/scalar"));

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
