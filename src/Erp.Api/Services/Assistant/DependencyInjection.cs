using Anthropic;
using Erp.Sales.Infrastructure.Application;

namespace Erp.Api.Services.Assistant;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the chat assistant. The Anthropic client is only created when an API key is
    /// configured, so a host without one starts normally and <see cref="AssistantService"/> reports
    /// itself as not configured.
    /// </summary>
    public static IServiceCollection AddAssistant(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(AssistantOptions.SectionName);
        services.Configure<AssistantOptions>(section);

        var options = section.Get<AssistantOptions>() ?? new AssistantOptions();

        if (options.IsConfigured)
            services.AddSingleton(new AnthropicClient { ApiKey = options.ApiKey });

        // The tools read through the analytics service of the Sales module, so they follow its scope.
        services.AddScoped<AssistantTools>();
        services.AddScoped<AssistantService>();

        return services;
    }
}
