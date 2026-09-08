using Erp.Core.Application;
using Erp.Core.Storage;
using Erp.FiscalPT;
using Erp.Inventory.Application;
using Erp.Inventory.Storage;
using Erp.Notification.Application;
using Erp.Notification.Storage;
using Erp.Purchasing.Application;
using Erp.Purchasing.Storage;
using Erp.Sales.Application;
using Erp.Sales.Storage;
using Erp.SeriesRegistry;
using Erp.Storage;

namespace Erp.Api;

/// <summary>
/// Every business module, registered in the order they depend on each other.
/// </summary>
/// <remarks>
/// This lives apart from <c>Program.cs</c> so the integration tests can build the same container the
/// host builds. A test that wired the modules itself would be testing its own wiring, and would go
/// on passing after production drifted away from it.
/// </remarks>
public static class ErpModules
{
    /// <param name="services">The host's service collection.</param>
    /// <param name="configuration">Carries the connection string and the fiscal settings.</param>
    /// <param name="allowDevelopmentKeyGeneration">
    /// Lets the signing key be generated locally when none is configured. True in development and
    /// in tests; false anywhere a real certificate is expected.
    /// </param>
    public static IServiceCollection AddErpModules(
        this IServiceCollection services,
        IConfiguration configuration,
        bool allowDevelopmentKeyGeneration)
    {
        // One context for every business module: they share a database, so sharing the context is
        // what lets a document, the stock it moves and the order it came from be written in one
        // transaction. Each module then adds its own tables to that model, and its services.
        services.AddErpStorage(configuration);

        services.AddCoreStorage();
        services.AddCoreApplication();

        // The series registry comes before the modules that take numbers from it.
        services.AddSeries();

        // What every module that issues fiscal documents shares: the signing key, the signer, and
        // the SAF-T exporter, which collects from whichever modules register an ISaftDocumentSource.
        services.AddFiscalPT(configuration, allowDevelopmentKeyGeneration);

        services.AddSalesStorage();
        services.AddSalesApplication();

        services.AddInventoryStorage();
        services.AddInventoryApplication();

        services.AddPurchasingStorage();
        services.AddPurchasingApplication();

        services.AddNotificationStorage();
        services.AddNotificationApplication(configuration);

        return services;
    }
}
