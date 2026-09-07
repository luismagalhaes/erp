using Erp.Purchasing.Application.Services;
using Erp.Purchasing.Infrastructure.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Purchasing.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddPurchasingApplication(this IServiceCollection services)
    {
        services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
        services.AddScoped<IGoodsReceiptService, GoodsReceiptService>();
        services.AddScoped<IPurchaseInvoiceService, PurchaseInvoiceService>();
        services.AddScoped<ISupplierReturnService, SupplierReturnService>();

        return services;
    }
}
