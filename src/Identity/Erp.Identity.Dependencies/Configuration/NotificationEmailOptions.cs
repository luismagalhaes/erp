namespace Erp.Identity.Dependencies.Configuration;

public sealed class NotificationEmailOptions
{
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Shared key sent to the notification service, which has no user token on this call.
    /// Supply it through user secrets or a secret store, never in appsettings.
    /// </summary>
    public string? InternalApiKey { get; set; }
}
