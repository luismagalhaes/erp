using Erp.FiscalPT.Saft;
using Erp.Sales.Application.Services;
using Erp.Sales.Infrastructure.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Sales.Application;

public static class DependencyInjection
{
    /// <summary>
    /// The signing key and the fiscal settings are not registered here: they belong to
    /// <c>AddFiscalPT</c>, because Purchasing signs self-billed invoices with the same key.
    /// </summary>
    public static IServiceCollection AddSalesApplication(this IServiceCollection services)
    {
        services.AddScoped<ISalesDocumentService, SalesDocumentService>();
        services.AddScoped<IStockMovementService, StockMovementService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<ISaftSummaryService, SaftSummaryService>();

        // What this module puts into the billing SAF-T. Registered as one source among however
        // many there turn out to be.
        services.AddScoped<ISaftDocumentSource, SalesSaftSource>();

        return services;
    }
}
