namespace Erp.Identity.Dependencies.Configuration;

/// <summary>
/// Client credentials this host uses to obtain its own access token. The secret comes from
/// user secrets or a secret store, never from appsettings.
/// </summary>
public sealed class ServiceAuthenticationOptions
{
    public const string SectionName = "ErpIdentityClient";

    /// <summary>Token issuer. Defaults to this host, which is the issuer.</summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>Overrides the token endpoint when it is not the Duende default.</summary>
    public string? TokenEndpoint { get; set; }

    public string ClientId { get; set; } = string.Empty;

    /// <summary>In the vault as ERPIDENTITYCLIENT__CLIENTSECRET, one value per environment.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Scope requested for outgoing service calls.</summary>
    public string Scope { get; set; } = "erp.notification.send";

    /// <summary>How long before expiry a cached token is renewed.</summary>
    public int RenewBeforeExpirySeconds { get; set; } = 60;

    public Uri ResolveTokenEndpoint()
    {
        if (!string.IsNullOrWhiteSpace(TokenEndpoint))
            return new Uri(TokenEndpoint);

        if (string.IsNullOrWhiteSpace(Authority))
            throw new InvalidOperationException($"{SectionName}:Authority is not configured.");

        return new Uri($"{Authority.TrimEnd('/')}/connect/token");
    }
}
