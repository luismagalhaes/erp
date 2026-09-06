using Erp.Notification.Infrastructure.Storage;
using Erp.Notification.Storage.Data;
using Erp.Notification.Storage.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Notification.Storage;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("NotificationDb")
            ?? throw new InvalidOperationException("Connection string 'NotificationDb' not found.");

        services.AddDbContext<NotificationDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IEmailNotificationStorage, EmailNotificationStorage>();

        return services;
    }
}
