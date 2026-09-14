using Infisical.Sdk;
using Infisical.Sdk.Model;
using Microsoft.Extensions.Configuration;

namespace Erp.Identity.Common.Configuration;

/// <summary>
/// Fetches this host's secrets from Infisical once, at startup, and layers them over whatever
/// appsettings.*.json already declared — the same role the built-in Azure Key Vault provider plays,
/// just against Infisical instead.
/// </summary>
internal sealed class InfisicalConfigurationProvider(InfisicalConfigurationSource source) : ConfigurationProvider
{
    // ConfigurationProvider.Load() is synchronous by contract — every other provider (including the
    // built-in Azure Key Vault one) blocks on its async fetch here rather than during a request.
    public override void Load()
    {
        try
        {
            LoadAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Could not load secrets from Infisical (project '{source.ProjectId}', environment " +
                $"'{source.EnvironmentSlug}', path '{source.SecretPath}'). Check Infisical:ClientId, " +
                "Infisical:ClientSecret and Infisical:ProjectId, and that the machine identity has " +
                "access to that project and environment.",
                ex);
        }
    }

    private async Task LoadAsync()
    {
        var settingsBuilder = new InfisicalSdkSettingsBuilder();
        if (!string.IsNullOrWhiteSpace(source.SiteUrl))
            settingsBuilder = settingsBuilder.WithHostUri(source.SiteUrl);

        var client = new InfisicalClient(settingsBuilder.Build());

        await client.Auth().UniversalAuth().LoginAsync(source.ClientId, source.ClientSecret);

        var secrets = await client.Secrets().ListAsync(new ListSecretsOptions
        {
            ProjectId = source.ProjectId,
            EnvironmentSlug = source.EnvironmentSlug,
            SecretPath = source.SecretPath
        });

        // Infisical secret names follow the same double-underscore convention .NET uses for
        // environment variables (e.g. CONNECTIONSTRINGS__IDENTITYDB), so they overlay
        // appsettings.json's nested sections without needing a naming scheme of their own.
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var secret in secrets)
            data[secret.SecretKey.Replace("__", ConfigurationPath.KeyDelimiter, StringComparison.Ordinal)] = secret.SecretValue;

        Data = data;
    }
}
