using Erp.Sales.Infrastructure.Storage;
using Erp.Sales.Storage.Data;
using Erp.Sales.Storage.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Sales.Storage;

public static class DependencyInjection
{
    public static IServiceCollection AddSalesStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SalesDb")
            ?? throw new InvalidOperationException("Connection string 'SalesDb' not found.");

        services.AddDbContext<SalesDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<ISeriesStorage, SeriesStorage>();
        services.AddScoped<ISalesDocumentStorage, SalesDocumentStorage>();
        services.AddScoped<IProductStorage, ProductStorage>();
        services.AddScoped<ISalesUnitOfWork, SalesUnitOfWork>();

        return services;
    }
}
