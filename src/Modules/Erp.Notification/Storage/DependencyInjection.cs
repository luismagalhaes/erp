using Erp.Notification.Infrastructure.Storage;
using Erp.Notification.Storage.Data;
using Erp.Notification.Storage.Storage;
using Erp.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Notification.Storage;

public static class DependencyInjection
{
    /// <summary>
    /// The module's tables and storages. The context itself belongs to the host, which registers it
    /// once with <c>AddStorage</c>.
    /// </summary>
    public static IServiceCollection AddNotificationStorage(this IServiceCollection services)
    {
        services.AddModuleModel<NotificationModelConfiguration>();

        services.AddScoped<IEmailNotificationStorage, EmailNotificationStorage>();

        return services;
    }
}
