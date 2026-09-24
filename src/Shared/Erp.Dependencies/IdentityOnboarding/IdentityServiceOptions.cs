namespace Erp.Dependencies.IdentityOnboarding;

/// <summary>
/// How this API reaches the Identity host on its own behalf, through the client credentials flow,
/// as the <c>erp-api</c> client. The secret comes from the vault, never from appsettings.
/// </summary>
public sealed class IdentityServiceOptions
{
    public const string SectionName = "ErpApiClient";

    /// <summary>
    /// The Identity host: token issuer, and the same process that serves the onboarding endpoints.
    /// </summary>
    public string Authority { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    /// <summary>In the vault as ERPAPICLIENT__CLIENTSECRET, one value per environment.</summary>
    public string ClientSecret { get; set; } = string.Empty;
}
