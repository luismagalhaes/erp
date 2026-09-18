using Erp.Core.Application.Services;
using Erp.Core.Infrastructure.Application;
using Erp.FiscalPT.AtWebservice;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Core.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddCoreApplication(this IServiceCollection services)
    {
        services.AddScoped<ICompanyAdminService, CompanyAdminService>();

        // Same instance behind both interfaces per scope: write side for the backoffice, read side
        // for Erp.Sales's AT communication — see the class doc comment.
        services.AddScoped<CompanyAtCredentialService>();
        services.AddScoped<ICompanyAtCredentialService>(sp => sp.GetRequiredService<CompanyAtCredentialService>());
        services.AddScoped<IAtCompanyProfileProvider>(sp => sp.GetRequiredService<CompanyAtCredentialService>());

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
        services.AddScoped<IVatRateService, VatRateService>();
        services.AddScoped<IEcoFeeTypeService, EcoFeeTypeService>();

        return services;
    }
}
