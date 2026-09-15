using Erp.Notification.Application.Configuration;
using Erp.Notification.Application.Services;
using Erp.Notification.Infrastructure.Application;
using Erp.Notification.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Notification.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SmtpOptions>(configuration.GetSection("Smtp"));
        services.Configure<NotificationWorkerOptions>(configuration.GetSection("NotificationWorker"));

        services.AddScoped<IEmailNotificationService, EmailNotificationService>();
        services.AddScoped<IEmailHistoryService, EmailHistoryService>();
        services.AddScoped<INotificationProcessingService, NotificationProcessingService>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddHostedService<EmailQueueWorker>();

        return services;
    }
}
