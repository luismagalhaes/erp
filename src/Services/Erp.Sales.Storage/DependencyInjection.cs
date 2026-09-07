using Erp.Common;
using Erp.Sales.Infrastructure.Storage;
using Erp.Sales.Storage.Data;
using Erp.Sales.Storage.Storage;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Erp.Sales.Storage;

public static class DependencyInjection
{
    public static IServiceCollection AddSalesStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ErpDb")
            ?? throw new InvalidOperationException("Connection string 'ErpDb' not found.");

        // All modules share one database and, within a request, one connection: that is what lets
        // a document and the stock it moves be written in the same transaction. Whichever module
        // registers first wins, and the rest join it.
        services.TryAddScoped(_ => new SharedDbConnection(new SqlConnection(connectionString)));
        services.TryAddScoped<IAmbientDbTransaction, AmbientDbTransaction>();

        // Each module keeps its own migrations history table so their migrations stay independent.
        services.AddDbContext<SalesDbContext>((sp, options) =>
            options.UseSqlServer(
                sp.GetRequiredService<SharedDbConnection>().Connection,
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory_Sales")));

        services.AddScoped<ISeriesStorage, SeriesStorage>();
        services.AddScoped<ISalesDocumentStorage, SalesDocumentStorage>();
        services.AddScoped<IStockMovementStorage, StockMovementStorage>();
        services.AddScoped<IPaymentStorage, PaymentStorage>();
        services.AddScoped<ISalesUnitOfWork, SalesUnitOfWork>();

        return services;
    }
}
