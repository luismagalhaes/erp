using Erp.Identity.Dependencies.Configuration;
using Erp.Identity.Dependencies.Services;
using Erp.Identity.Infrastructure.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Erp.Identity.Dependencies;

public static class DependencyInjection
{
    /// <param name="developmentClientSecret">
    /// Secret used when none is configured, so a local machine works without setting up user
    /// secrets. Pass null outside development: without a secret the host cannot call other services.
    /// </param>
    public static IServiceCollection AddIdentityDependencies(
        this IServiceCollection services,
        IConfiguration configuration,
        string? developmentClientSecret = null)
    {
        services.Configure<NotificationEmailOptions>(configuration.GetSection("NotificationService"));

        services.Configure<ServiceAuthenticationOptions>(options =>
        {
            configuration.GetSection(ServiceAuthenticationOptions.SectionName).Bind(options);

            if (string.IsNullOrWhiteSpace(options.ClientSecret) && !string.IsNullOrWhiteSpace(developmentClientSecret))
                options.ClientSecret = developmentClientSecret;
        });

        // Plain client used only to reach the token endpoint, so it carries no handler of its own.
        services.AddHttpClient(ClientCredentialsTokenProvider.HttpClientName);

        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<IServiceTokenProvider, ClientCredentialsTokenProvider>();
        services.AddTransient<ServiceTokenHandler>();

        services.AddHttpClient<INotificationEmailClient, NotificationEmailClient>((serviceProvider, httpClient) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<NotificationEmailOptions>>().Value;

            if (string.IsNullOrWhiteSpace(options.BaseUrl))
                throw new InvalidOperationException("NotificationService:BaseUrl is not configured.");

            httpClient.BaseAddress = new Uri(options.BaseUrl);
        })
        .AddHttpMessageHandler<ServiceTokenHandler>();

        return services;
    }
}
