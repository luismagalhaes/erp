using Erp.FiscalPT.Saft;
using Erp.FiscalPT.Signing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.FiscalPT;

/// <summary>
/// Registers what every module that issues fiscal documents shares: the signing key, the signer,
/// and the SAF-T exporter.
/// </summary>
/// <remarks>
/// This is the one place in the library that knows about dependency injection. Everything else is
/// plain classes and functions, so the rules can be tested without a container.
/// </remarks>
public static class DependencyInjection
{
    /// <param name="allowDevelopmentKeyGeneration">
    /// Set from the environment by the host. Outside development it must stay false, so a missing
    /// key fails loudly instead of silently signing with a throwaway one.
    /// </param>
    public static IServiceCollection AddFiscalPT(
        this IServiceCollection services,
        IConfiguration configuration,
        bool allowDevelopmentKeyGeneration = false)
    {
        services.Configure<FiscalOptions>(options =>
        {
            configuration.GetSection(FiscalOptions.SectionName).Bind(options);
            options.AllowDevelopmentKeyGeneration = allowDevelopmentKeyGeneration;
        });

        // Singleton because the key is loaded once and reused; the signature itself is stateless.
        services.AddSingleton<ISigningKeyProvider, SigningKeyProvider>();
        services.AddScoped<IDocumentSigner, DocumentSigner>();

        // The exporter collects from every ISaftDocumentSource the modules register.
        services.AddScoped<SaftExporter>();

        return services;
    }
}
