using Erp.Sales.Infrastructure.Storage;
using Erp.Sales.Storage.Data;
using Erp.Sales.Storage.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Sales.Storage;

public static class DependencyInjection
{
    public static IServiceCollection AddSalesStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ErpDb")
            ?? throw new InvalidOperationException("Connection string 'ErpDb' not found.");

        // All modules share one database. Each keeps its own migrations history table so their
        // migrations stay independent.
        services.AddDbContext<SalesDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory_Sales")));

        services.AddScoped<ISeriesStorage, SeriesStorage>();
        services.AddScoped<ISalesDocumentStorage, SalesDocumentStorage>();
        services.AddScoped<IStockMovementStorage, StockMovementStorage>();
        services.AddScoped<ISalesUnitOfWork, SalesUnitOfWork>();

        return services;
    }
}
