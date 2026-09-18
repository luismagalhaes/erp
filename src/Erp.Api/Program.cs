using Erp.Api.Security;
using Erp.Api.Services;
using Erp.Api.Telemetry;
using Erp.Common;
using Erp.Common.Configuration;
using Erp.Dependencies;
using Erp.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.OData;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddInfisicalSecrets(builder.Environment);

builder.Configuration.EnsureConfigured(
    "ConnectionStrings:ErpDb",
    "IdentityServer:Authority",
    "AT:SigningKeyPem",
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

// Free, keyless external lookups (postal codes, VIES NIF validation) used by the master data
// editors; they carry no per-module dependency, so they are registered once for the whole host.
builder.Services.AddErpDependencies(builder.Configuration);

// OData is used only as a query language over the existing REST routes: it lets the data grids
// push filtering, sorting and paging down to SQL instead of loading everything into the client.
// The logging filter runs once per action instead of every action logging its own entry — see
// RequestLoggingFilter. RequireCompanyAccessFilter runs the same way, once for every action, so
// no controller has to check on its own whether the caller actually belongs to the companyId it
// asked for — see its own doc comment for why that check cannot live in the database layer.
// TenantAwareNotFoundFilter covers what that one cannot: a {id:guid} action carries no companyId
// of its own to check before running, so it turns an already-safe 404 (the tenant filter already
// hid the row) into an explicit 403 when the row exists for another company.
builder.Services.AddControllers(options =>
    {
        options.Filters.Add<RequestLoggingFilter>();
        options.Filters.Add<RequireCompanyAccessFilter>();
        options.Filters.Add<TenantAwareNotFoundFilter>();
    })
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

// AppDbContext's tenant filter (see AppDbContext.ApplyTenantFilters) needs to know who the caller
// is; CurrentUserContext carries that per request, and CurrentUserContextMiddleware fills it in
// before any controller runs. Registered as itself and as ICurrentUserContext so the middleware —
// which needs the settable Load method the interface does not expose — and AppDbContext each get
// the one instance already scoped to this request, overriding Erp.Storage's own always-unrestricted
// default (see AddStorage) now that a real, HTTP aware one exists.
builder.Services.AddScoped<CurrentUserContext>();
builder.Services.AddScoped<ICurrentUserContext>(sp => sp.GetRequiredService<CurrentUserContext>());

// Encrypts the per-company AT WDT password at rest (see CompanyAtCredentialService). A fixed
// application name keeps the key ring stable across deploys instead of tying it to the content
// root path, which can differ between deployment slots.
builder.Services.AddDataProtection()
    .SetApplicationName(Constants.ApiResources.ErpApi);

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

// After authorization: a request that was going to be rejected never pays for the membership
// query, and every request that reaches it already carries an authorized principal.
app.UseMiddleware<CurrentUserContextMiddleware>();

app.MapControllers();

await app.RunAsync();
