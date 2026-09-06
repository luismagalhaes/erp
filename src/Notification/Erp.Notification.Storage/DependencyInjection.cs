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
        var connectionString = configuration.GetConnectionString("ErpDb")
            ?? throw new InvalidOperationException("Connection string 'ErpDb' not found.");

        // All modules share one database. Each keeps its own migrations history table so their
        // migrations stay independent.
        services.AddDbContext<NotificationDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory_Notification")));

        services.AddScoped<IEmailNotificationStorage, EmailNotificationStorage>();

        return services;
    }
}
