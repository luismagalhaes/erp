using Erp.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }

    /// <summary>Adds a module's table declarations to the shared model.</summary>
    public static IServiceCollection AddModuleModel<TConfiguration>(this IServiceCollection services)
        where TConfiguration : class, IModuleModelConfiguration =>
        services.AddSingleton<IModuleModelConfiguration, TConfiguration>();
}
