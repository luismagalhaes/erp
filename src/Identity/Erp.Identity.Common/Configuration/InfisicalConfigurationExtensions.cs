using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Erp.Identity.Common.Configuration;

public static class InfisicalConfigurationExtensions
{
    // The slugs Infisical assigns a new project's three default environments — not something to
    // guess from ASPNETCORE_ENVIRONMENT, but the two happen to agree once "Development" is lowered
    // to "development"... except Infisical shortens it to "dev", hence the explicit map.
    private static readonly Dictionary<string, string> DefaultEnvironmentSlugs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Development"] = "dev",
        ["Staging"] = "staging",
        ["Production"] = "prod"
    };

    /// <summary>
    /// Layers this host's secrets from Infisical over <c>appsettings.*.json</c>, so a real connection
    /// string or admin password never has to live in a committed file.
    /// </summary>
    /// <remarks>
    /// Call this right after <c>CreateBuilder</c>, before anything reads <paramref name="configuration"/>
    /// — it has to be the last source added so it wins over appsettings.*.json and environment
    /// variables for the same key.
    /// <para>
    /// Does nothing when <c>Infisical:ClientId</c>, <c>Infisical:ClientSecret</c> or
    /// <c>Infisical:ProjectId</c> is unset, which is the case for plain local development: nobody
    /// has to have Infisical credentials just to run the app against LocalDB. Those three only ever
    /// belong in an environment variable or, on Azure, the Web App's Application Settings — the same
    /// place <c>ASPNETCORE_ENVIRONMENT</c> already lives — never in <c>appsettings.*.json</c>, or
    /// this would just move the secret it's meant to remove.
    /// </para>
    /// <para>
    /// This is Erp.Identity's own copy: the Identity host and its projects deliberately do not
    /// reference <c>Erp.Common</c> — <c>Erp.Identity.Common</c> is where anything they need to share
    /// lives instead, kept independent even at the cost of a small duplication with the copy in
    /// <c>Erp.Common.Configuration</c> that Erp.Api/Erp.Main use.
    /// </para>
    /// </remarks>
    public static void AddInfisicalSecrets(this ConfigurationManager configuration, IHostEnvironment environment)
    {
        var clientId = configuration["Infisical:ClientId"];
        var clientSecret = configuration["Infisical:ClientSecret"];
        var projectId = configuration["Infisical:ProjectId"];

        var configured = new[] { clientId, clientSecret, projectId };
        if (configured.All(string.IsNullOrWhiteSpace))
            return;

        if (configured.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException(
                "Infisical is partially configured: Infisical:ClientId, Infisical:ClientSecret and " +
                "Infisical:ProjectId must be set together, or not at all.");

        var environmentSlug = configuration["Infisical:Environment"]
            ?? (DefaultEnvironmentSlugs.TryGetValue(environment.EnvironmentName, out var slug)
                ? slug
                : environment.EnvironmentName.ToLowerInvariant());

        configuration.Sources.Add(new InfisicalConfigurationSource
        {
            ClientId = clientId!,
            ClientSecret = clientSecret!,
            ProjectId = projectId!,
            EnvironmentSlug = environmentSlug,
            SecretPath = configuration["Infisical:SecretPath"] ?? "/",
            SiteUrl = configuration["Infisical:SiteUrl"]
        });
    }
}
