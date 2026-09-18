using Erp.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Erp.Storage;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the one context the business modules share. Call it before the modules, which then
    /// add their own <see cref="IModuleModelConfiguration"/> and storages.
    /// </summary>
    public static IServiceCollection AddStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ErpDb")
            ?? throw new InvalidOperationException("Connection string 'ErpDb' not found.");

        // A free-tier Azure SQL Database auto-pauses after inactivity, and the connection that wakes
        // it back up can otherwise exceed SqlClient's 15s default before it finishes resuming.
        connectionString = new SqlConnectionStringBuilder(connectionString) { ConnectTimeout = 30 }.ConnectionString;

        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ITenantExistenceChecker>(sp => sp.GetRequiredService<AppDbContext>());

        // TryAdd: a host that knows about HTTP requests (Erp.Api) overrides this with one that
        // resolves the caller's own companies; anything else — migrations, background workers, the
        // integration tests that call AddStorage directly — keeps this always unrestricted default.
        services.TryAddScoped<ICurrentUserContext, NullCurrentUserContext>();

        return services;
    }

    /// <summary>Adds a module's table declarations to the shared model.</summary>
    public static IServiceCollection AddModuleModel<TConfiguration>(this IServiceCollection services)
        where TConfiguration : class, IModuleModelConfiguration =>
        services.AddSingleton<IModuleModelConfiguration, TConfiguration>();
}
