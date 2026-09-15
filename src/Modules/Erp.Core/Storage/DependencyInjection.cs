using Erp.Core.Infrastructure.Storage;
using Erp.Core.Storage.Data;
using Erp.Core.Storage.Storage;
using Erp.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Core.Storage;

public static class DependencyInjection
{
    /// <summary>
    /// The module's tables and storages. The context itself belongs to the host, which registers it
    /// once with <c>AddStorage</c>.
    /// </summary>
    public static IServiceCollection AddCoreStorage(this IServiceCollection services)
    {
        services.AddModuleModel<CoreModelConfiguration>();

        services.AddScoped<ICompanyStorage, CompanyStorage>();
        services.AddScoped<IUserCompanyStorage, UserCompanyStorage>();
        services.AddScoped<IBrandStorage, BrandStorage>();
        services.AddScoped<IProductFamilyStorage, ProductFamilyStorage>();
        services.AddScoped<IProductSubfamilyStorage, ProductSubfamilyStorage>();
        services.AddScoped<IProductStorage, ProductStorage>();
        services.AddScoped<ICustomerStorage, CustomerStorage>();
        services.AddScoped<ISupplierStorage, SupplierStorage>();
        services.AddScoped<IWarehouseStorage, WarehouseStorage>();
        services.AddScoped<IVatRateStorage, VatRateStorage>();

        return services;
    }
}
