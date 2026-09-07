using Erp.Common;
using Erp.Inventory.Infrastructure.Storage;
using Erp.Inventory.Storage.Data;
using Erp.Inventory.Storage.Storage;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Erp.Inventory.Storage;

public static class DependencyInjection
{
    public static IServiceCollection AddInventoryStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ErpDb")
            ?? throw new InvalidOperationException("Connection string 'ErpDb' not found.");

        // Shared with the other modules, so stock can be written inside the transaction that is
        // already writing the document.
        services.TryAddScoped(_ => new SharedDbConnection(new SqlConnection(connectionString)));
        services.TryAddScoped<IAmbientDbTransaction, AmbientDbTransaction>();

        // Each module keeps its own migrations history table so their migrations stay independent.
        services.AddDbContext<InventoryDbContext>((sp, options) =>
            options.UseSqlServer(
                sp.GetRequiredService<SharedDbConnection>().Connection,
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory_Inventory")));

        services.AddScoped<IStockStorage, StockStorage>();
        services.AddScoped<IInventoryCountStorage, InventoryCountStorage>();
        services.AddScoped<IInventoryUnitOfWork, InventoryUnitOfWork>();

        return services;
    }
}
