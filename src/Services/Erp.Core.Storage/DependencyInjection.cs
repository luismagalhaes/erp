using Erp.Core.Infrastructure.Storage;
using Erp.Core.Storage.Data;
using Erp.Core.Storage.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Core.Storage;

public static class DependencyInjection
{
    public static IServiceCollection AddCoreStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("CoreDb")
            ?? throw new InvalidOperationException("Connection string 'CoreDb' not found.");

        services.AddDbContext<CoreDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<ICompanyStorage, CompanyStorage>();
        services.AddScoped<IUserCompanyStorage, UserCompanyStorage>();

        return services;
    }
}
