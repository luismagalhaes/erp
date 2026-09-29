using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Erp.Api.Services.Assistant;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the chat assistant on top of <see cref="IChatClient"/>. The client is only created
    /// when an endpoint and a model are configured, so a host without them starts normally and
    /// <see cref="AssistantService"/> reports itself as not configured.
    /// </summary>
    public static IServiceCollection AddAssistant(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(AssistantOptions.SectionName);
        services.Configure<AssistantOptions>(section);

        var options = section.Get<AssistantOptions>() ?? new AssistantOptions();

        if (options.IsConfigured)
        {
            services.AddSingleton<IChatClient>(provider =>
                new ChatClientBuilder(
                        new OpenAIClient(
                                new ApiKeyCredential(string.IsNullOrWhiteSpace(options.ApiKey) ? "none" : options.ApiKey),
                                new OpenAIClientOptions { Endpoint = new Uri(options.Endpoint!) })
                            .GetChatClient(options.Model)
                            .AsIChatClient())
                    .UseFunctionInvocation(
                        provider.GetRequiredService<ILoggerFactory>(),
                        client => client.MaximumIterationsPerRequest = options.MaxToolRounds)
                    .Build());
        }

        // The tools read through the analytics service of the Sales module, so they follow its scope.
        services.AddScoped<AssistantTools>();
        services.AddScoped<AssistantService>();

        return services;
    }
}
