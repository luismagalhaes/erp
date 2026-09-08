using Erp.Inventory.Infrastructure.Storage;
using Erp.Inventory.Storage.Data;
using Erp.Inventory.Storage.Storage;
using Erp.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Inventory.Storage;

public static class DependencyInjection
{
    /// <summary>
    /// The module's tables and storages. The context itself belongs to the host, which registers it
    /// once with <c>AddErpStorage</c>.
    /// </summary>
    public static IServiceCollection AddInventoryStorage(this IServiceCollection services)
    {
        services.AddModuleModel<InventoryModelConfiguration>();

        services.AddScoped<IStockStorage, StockStorage>();
        services.AddScoped<IInventoryCountStorage, InventoryCountStorage>();

        return services;
    }
}
