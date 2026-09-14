using Microsoft.Extensions.Configuration;

namespace Erp.Common.Configuration;

public static class RequiredConfigurationExtensions
{
    /// <summary>
    /// Fails startup immediately, naming every missing key at once — rather than the host coming up
    /// and only failing hours later, one setting at a time, the first time each is actually read
    /// (a connection string on the first request, a signing key on the first document, an admin
    /// password on the first seed).
    /// </summary>
    public static void EnsureConfigured(this IConfiguration configuration, params string[] requiredKeys)
    {
        var missing = requiredKeys.Where(key => string.IsNullOrWhiteSpace(configuration[key])).ToArray();

        if (missing.Length == 0)
            return;

        throw new InvalidOperationException(
            "Missing required configuration. Set these in appsettings, an environment variable, or the vault:" +
            string.Concat(missing.Select(key => $"{Environment.NewLine}  - {key}")));
    }
}
