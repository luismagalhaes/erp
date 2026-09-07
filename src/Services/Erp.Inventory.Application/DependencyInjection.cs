using Erp.Inventory.Application.Services;
using Erp.Inventory.Infrastructure.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Inventory.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddInventoryApplication(this IServiceCollection services)
    {
        services.AddScoped<IStockService, StockService>();
        services.AddScoped<IStockRecorder, StockRecorder>();
        services.AddScoped<IInventoryFileService, InventoryFileService>();
        services.AddScoped<IInventoryCountService, InventoryCountService>();

        return services;
    }
}
