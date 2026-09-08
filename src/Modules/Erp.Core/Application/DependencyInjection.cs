using Erp.Core.Application.Services;
using Erp.Core.Infrastructure.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Core.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddCoreApplication(this IServiceCollection services)
    {
        services.AddScoped<ICompanyAdminService, CompanyAdminService>();
        services.AddScoped<IUserCompanyService, UserCompanyService>();
        services.AddScoped<IUserCompanyAdminService, UserCompanyAdminService>();

        // Master data shared by every module.
        services.AddScoped<IBrandService, BrandService>();
        services.AddScoped<IProductFamilyService, ProductFamilyService>();
        services.AddScoped<IProductSubfamilyService, ProductSubfamilyService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IWarehouseService, WarehouseService>();

        return services;
    }
}
