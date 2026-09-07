using Erp.Sales.Application.Configuration;
using Erp.Sales.Application.Services;
using Erp.Sales.Application.Signing;
using Erp.Sales.Infrastructure.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Sales.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddSalesApplication(
        this IServiceCollection services,
        IConfiguration configuration,
        bool allowDevelopmentKeyGeneration = false)
    {
        services.Configure<FiscalOptions>(options =>
        {
            configuration.GetSection(FiscalOptions.SectionName).Bind(options);
            options.AllowDevelopmentKeyGeneration = allowDevelopmentKeyGeneration;
        });

        services.AddSingleton<ISigningKeyProvider, SigningKeyProvider>();
        services.AddScoped<IDocumentSigner, DocumentSigner>();
        services.AddScoped<ISalesDocumentService, SalesDocumentService>();
        services.AddScoped<ISeriesService, SeriesService>();
        services.AddScoped<IStockMovementService, StockMovementService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<ISaftExportService, SaftExportService>();

        return services;
    }
}
