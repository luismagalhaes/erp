using Erp.Common;
using Erp.Purchasing.Infrastructure.Storage;
using Erp.Purchasing.Storage.Data;
using Erp.Purchasing.Storage.Storage;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Erp.Purchasing.Storage;

public static class DependencyInjection
{
    public static IServiceCollection AddPurchasingStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ErpDb")
            ?? throw new InvalidOperationException("Connection string 'ErpDb' not found.");

        // Shared with the other modules, so a goods receipt and the stock it brings in are written
        // in one transaction.
        services.TryAddScoped(_ => new SharedDbConnection(new SqlConnection(connectionString)));
        services.TryAddScoped<IAmbientDbTransaction, AmbientDbTransaction>();

        // Each module keeps its own migrations history table so their migrations stay independent.
        services.AddDbContext<PurchasingDbContext>((sp, options) =>
            options.UseSqlServer(
                sp.GetRequiredService<SharedDbConnection>().Connection,
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory_Purchasing")));

        services.AddScoped<IPurchaseOrderStorage, PurchaseOrderStorage>();
        services.AddScoped<IGoodsReceiptStorage, GoodsReceiptStorage>();
        services.AddScoped<IPurchaseInvoiceStorage, PurchaseInvoiceStorage>();
        services.AddScoped<ISupplierReturnStorage, SupplierReturnStorage>();
        services.AddScoped<IPurchasingUnitOfWork, PurchasingUnitOfWork>();

        return services;
    }
}
