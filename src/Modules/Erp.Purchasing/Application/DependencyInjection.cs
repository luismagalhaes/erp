using Erp.FiscalPT.Saft;
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
        services.AddScoped<ISelfBilledInvoiceService, SelfBilledInvoiceService>();
        services.AddScoped<ISupplierPaymentService, SupplierPaymentService>();
        services.AddScoped<ISupplierStatementService, SupplierStatementService>();

        // The self-billing SAF-T, one file per supplier. Registered alongside the Sales source; the
        // exporter tells them apart by file type, so neither can end up in the other's file.
        services.AddScoped<ISaftDocumentSource, SelfBillingSaftSource>();

        return services;
    }
}
