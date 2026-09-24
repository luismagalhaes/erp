namespace Erp.Identity.Dependencies.Configuration;

/// <summary>Where the ERP API is, so this host can queue emails on its notification module.</summary>
public sealed class NotificationEmailOptions
{
    public const string SectionName = "ErpApi";

    public string BaseUrl { get; set; } = string.Empty;
}
