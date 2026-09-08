using Erp.Purchasing.Infrastructure.Storage;
using Erp.Purchasing.Storage.Data;
using Erp.Purchasing.Storage.Storage;
using Erp.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Purchasing.Storage;

public static class DependencyInjection
{
    /// <summary>
    /// The module's tables and storages. The context itself belongs to the host, which registers it
    /// once with <c>AddErpStorage</c>.
    /// </summary>
    public static IServiceCollection AddPurchasingStorage(this IServiceCollection services)
    {
        services.AddModuleModel<PurchasingModelConfiguration>();

        services.AddScoped<IPurchaseOrderStorage, PurchaseOrderStorage>();
        services.AddScoped<IGoodsReceiptStorage, GoodsReceiptStorage>();
        services.AddScoped<IPurchaseInvoiceStorage, PurchaseInvoiceStorage>();
        services.AddScoped<ISupplierReturnStorage, SupplierReturnStorage>();
        services.AddScoped<ISelfBilledInvoiceStorage, SelfBilledInvoiceStorage>();

        return services;
    }
}
