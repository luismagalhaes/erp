using Erp.Core.Infrastructure.Storage;
using Erp.Core.Storage.Data;
using Erp.Core.Storage.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Core.Storage;

public static class DependencyInjection
{
    public static IServiceCollection AddCoreStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ErpDb")
            ?? throw new InvalidOperationException("Connection string 'ErpDb' not found.");

        // All modules share one database. Each keeps its own migrations history table so their
        // migrations stay independent.
        services.AddDbContext<CoreDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory_Core")));

        services.AddScoped<ICompanyStorage, CompanyStorage>();
        services.AddScoped<IUserCompanyStorage, UserCompanyStorage>();
        services.AddScoped<IBrandStorage, BrandStorage>();
        services.AddScoped<IProductFamilyStorage, ProductFamilyStorage>();
        services.AddScoped<IProductSubfamilyStorage, ProductSubfamilyStorage>();
        services.AddScoped<IProductStorage, ProductStorage>();
        services.AddScoped<ICustomerStorage, CustomerStorage>();
        services.AddScoped<ISupplierStorage, SupplierStorage>();

        return services;
    }
}
