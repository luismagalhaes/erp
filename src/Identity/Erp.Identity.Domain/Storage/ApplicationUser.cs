using Microsoft.AspNetCore.Identity;

namespace Erp.Identity.Data;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}".Trim();
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The user's preferred UI culture (e.g. "pt-PT", "en-US"). Issued as the "locale" claim so
    /// every ERP UI can localize without a round trip to the Identity host. Defaults to pt-PT,
    /// matching <c>Constants.Localization.DefaultCulture</c> in <c>Erp.Identity.Common</c>.
    /// </summary>
    public string PreferredLanguage { get; set; } = "pt-PT";
}
