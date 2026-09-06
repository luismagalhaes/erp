using Erp.Identity.Dependencies.Configuration;
using Erp.Identity.Dependencies.Services;
using Erp.Identity.Infrastructure.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Erp.Identity.Dependencies;

public static class DependencyInjection
{
    /// <summary>Header the notification service uses to authenticate service to service calls.</summary>
    public const string InternalApiKeyHeader = "X-Internal-Api-Key";

    public static IServiceCollection AddIdentityDependencies(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<NotificationEmailOptions>(configuration.GetSection("NotificationService"));

        services.AddHttpClient<INotificationEmailClient, NotificationEmailClient>((serviceProvider, httpClient) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<NotificationEmailOptions>>().Value;

            if (string.IsNullOrWhiteSpace(options.BaseUrl))
                throw new InvalidOperationException("NotificationService:BaseUrl is not configured.");

            httpClient.BaseAddress = new Uri(options.BaseUrl);

            if (!string.IsNullOrWhiteSpace(options.InternalApiKey))
                httpClient.DefaultRequestHeaders.Add(InternalApiKeyHeader, options.InternalApiKey);
        });

        return services;
    }
}
