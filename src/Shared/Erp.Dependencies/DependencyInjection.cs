using Erp.Dependencies.IdentityOnboarding;
using Erp.Dependencies.PostalCodes;
using Erp.Dependencies.VatNumbers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Erp.Dependencies;

public static class DependencyInjection
{
    private const string DefaultPostalCodeBaseAddress = "https://moradas.dev/";
    private const string DefaultVatNumberValidationBaseAddress = "https://ec.europa.eu/taxation_customs/vies/rest-api/";

    /// <summary>
    /// Registers the free, keyless external lookups shared across modules: moradas.dev for
    /// postal codes, and the EU VIES REST API for company NIF/NIPC validation. Base addresses
    /// are read from configuration ("Dependencies:PostalCodeBaseAddress" and
    /// "Dependencies:VatNumberValidationBaseAddress"), falling back to the well-known public
    /// endpoints when not configured.
    /// </summary>
    public static IServiceCollection AddErpDependencies(this IServiceCollection services, IConfiguration configuration)
    {
        var postalCodeBaseAddress = configuration["Dependencies:PostalCodeBaseAddress"] ?? DefaultPostalCodeBaseAddress;
        var vatNumberValidationBaseAddress = configuration["Dependencies:VatNumberValidationBaseAddress"] ?? DefaultVatNumberValidationBaseAddress;

        services.AddHttpClient<IPostalCodeLookupService, PostalCodeLookupService>(httpClient =>
        {
            httpClient.BaseAddress = new Uri(postalCodeBaseAddress);
        });

        services.AddHttpClient<IVatNumberValidationService, VatNumberValidationService>(httpClient =>
        {
            httpClient.BaseAddress = new Uri(vatNumberValidationBaseAddress);
        });

        // The Identity host, reached on this API's own behalf (client credentials, as the erp-api
        // client, configured under ErpApiClient) to look people up
        // and invite them to a company.
        services.Configure<IdentityServiceOptions>(configuration.GetSection(IdentityServiceOptions.SectionName));

        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient(IdentityServiceTokenProvider.HttpClientName);
        services.AddSingleton<IdentityServiceTokenProvider>();
        services.AddTransient<IdentityServiceTokenHandler>();

        services.AddHttpClient<IIdentityOnboardingClient, IdentityOnboardingClient>((serviceProvider, httpClient) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<IdentityServiceOptions>>().Value;

                if (!string.IsNullOrWhiteSpace(options.Authority))
                    httpClient.BaseAddress = new Uri(options.Authority.TrimEnd('/') + "/");
            })
            .AddHttpMessageHandler<IdentityServiceTokenHandler>();

        return services;
    }
}
