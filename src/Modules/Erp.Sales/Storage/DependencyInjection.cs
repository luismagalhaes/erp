using Erp.Sales.Infrastructure.Storage;
using Erp.Sales.Storage.Data;
using Erp.Sales.Storage.Storage;
using Erp.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Sales.Storage;

public static class DependencyInjection
{
    /// <summary>
    /// The module's tables and storages. The context itself belongs to the host, which registers it
    /// once with <c>AddErpStorage</c>.
    /// </summary>
    public static IServiceCollection AddSalesStorage(this IServiceCollection services)
    {
        services.AddModuleModel<SalesModelConfiguration>();

        services.AddScoped<ISalesDocumentStorage, SalesDocumentStorage>();
        services.AddScoped<IStockMovementStorage, StockMovementStorage>();
        services.AddScoped<IPaymentStorage, PaymentStorage>();

        return services;
    }
}
