using Erp.Api.Services;
using Erp.Api.Telemetry;
using Erp.Common;
using Erp.Common.Configuration;
using Erp.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OData;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddInfisicalSecrets(builder.Environment);

builder.Configuration.EnsureConfigured(
    "ConnectionStrings:ErpDb",
    "IdentityServer:Authority",
    "Fiscal:PrivateKeyPem",
    "Smtp:Host",
    "Smtp:UserName",
    "Smtp:Password",
    "Smtp:FromEmail");

builder.AddErpOpenTelemetry(Constants.ApiResources.ErpApi);

// Every business module, in one call, so the integration tests can build the same container
// instead of a lookalike of their own.
builder.Services.AddModules(
    builder.Configuration,
    allowDevelopmentKeyGeneration: builder.Environment.IsDevelopment());

// OData is used only as a query language over the existing REST routes: it lets the data grids
// push filtering, sorting and paging down to SQL instead of loading everything into the client.
// The logging filter runs once per action instead of every action logging its own entry — see
// RequestLoggingFilter.
builder.Services.AddControllers(options => options.Filters.Add<RequestLoggingFilter>())
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
// Retried: a free-tier Azure SQL Database auto-pauses after inactivity, and this is the first
// connection of the process, so it is the one that can land while the database is still waking up.
await using (var scope = app.Services.CreateAsyncScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            await context.Database.MigrateAsync();
            break;
        }
        catch (SqlException ex) when (attempt < 5)
        {
            logger.LogWarning(
                ex, "Database not reachable yet on startup (attempt {Attempt}/5), retrying in 10s...", attempt);
            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }
}

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.Title = "ERP API";
});

app.MapGet("/", () => Results.Redirect("/scalar"));

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();
