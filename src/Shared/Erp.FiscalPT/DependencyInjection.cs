using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using Erp.FiscalPT.AtWebservice.Series;
using Erp.FiscalPT.AtWebservice.TransportDocuments;
using Erp.FiscalPT.Saft;
using Erp.FiscalPT.Signing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

            // Vault key is AT__SIGNINGKEYPEM, not FISCAL__SIGNINGKEYPEM — grouped with the other
            // AT credentials (AT__CLIENTCERTIFICATEBASE64, ...) instead of under Fiscal.
            options.SigningKeyPem = configuration[$"{AtOptions.SectionName}:{nameof(FiscalOptions.SigningKeyPem)}"];
            options.AllowDevelopmentKeyGeneration = allowDevelopmentKeyGeneration;
        });

        services.Configure<AtOptions>(configuration.GetSection(AtOptions.SectionName));

        // Singleton because the key is loaded once and reused; the signature itself is stateless.
        services.AddSingleton<ISigningKeyProvider, SigningKeyProvider>();
        services.AddScoped<IDocumentSigner, DocumentSigner>();

        // The exporter collects from every ISaftDocumentSource the modules register.
        services.AddScoped<SaftExporter>();

        services.AddHttpClient<IAtTransportDocumentClient, AtTransportDocumentClient>((sp, client) =>
        {
            var atOptions = sp.GetRequiredService<IOptions<AtOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(atOptions.TransportDocumentsUrl))
                client.BaseAddress = new Uri(atOptions.TransportDocumentsUrl);
        })
        .ConfigurePrimaryHttpMessageHandler(BuildClientCertificateHandler);

        // Same client certificate, WS-Security scheme and credential shape as the transport
        // documents client above — AT explicitly allows reusing that certificate here.
        services.AddHttpClient<IAtSeriesClient, AtSeriesClient>((sp, client) =>
        {
            var atOptions = sp.GetRequiredService<IOptions<AtOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(atOptions.SeriesUrl))
                client.BaseAddress = new Uri(atOptions.SeriesUrl);
        })
        .ConfigurePrimaryHttpMessageHandler(BuildClientCertificateHandler);

        return services;
    }

    /// <summary>
    /// AT authenticates the caller by client certificate on the transport, on top of the
    /// WS-Security header inside the SOAP body — shared by every AT webservice client.
    /// </summary>
    private static HttpClientHandler BuildClientCertificateHandler(IServiceProvider sp)
    {
        var atOptions = sp.GetRequiredService<IOptions<AtOptions>>().Value;
        var handler = new HttpClientHandler();

        if (!string.IsNullOrWhiteSpace(atOptions.ClientCertificateBase64))
        {
            var certificateBytes = Convert.FromBase64String(atOptions.ClientCertificateBase64);
            var certificate = X509CertificateLoader.LoadPkcs12(certificateBytes, atOptions.ClientCertificatePassword);

            // Manual is already the default once a certificate is added, but stated explicitly so
            // it is not left to an implicit default while this is being debugged.
            handler.ClientCertificateOptions = ClientCertificateOption.Manual;
            handler.ClientCertificates.Add(certificate);

            // Temporary: confirms the certificate actually carries what TLS client auth needs
            // (private key, not expired) before blaming the WS-Security header for an auth failure.
            // Remove once the AT connection is confirmed working.
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Erp.FiscalPT.AtWebservice.ClientCertificate");
            logger.LogInformation(
                "AT client certificate: Subject={Subject}, Thumbprint={Thumbprint}, HasPrivateKey={HasPrivateKey}, ValidFrom={NotBefore}, ValidTo={NotAfter}",
                certificate.Subject, certificate.Thumbprint, certificate.HasPrivateKey, certificate.NotBefore, certificate.NotAfter);
        }

        return handler;
    }
}
