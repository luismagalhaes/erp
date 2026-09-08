using Erp.SeriesRegistry.Application.Services;
using Erp.SeriesRegistry.Infrastructure.Application;
using Erp.SeriesRegistry.Infrastructure.Storage;
using Erp.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.SeriesRegistry;

public static class DependencyInjection
{
    /// <summary>
    /// The series registry: its table, its storage and its service. Registered before the modules
    /// that take numbers from it.
    /// </summary>
    public static IServiceCollection AddSeries(this IServiceCollection services)
    {
        services.AddModuleModel<Storage.SeriesModelConfiguration>();

        services.AddScoped<ISeriesStorage, Storage.SeriesStorage>();
        services.AddScoped<ISeriesService, SeriesService>();

        return services;
    }
}
