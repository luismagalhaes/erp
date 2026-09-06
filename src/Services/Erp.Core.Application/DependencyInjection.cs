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

        return services;
    }
}
