namespace Erp.Identity.Storage.Configuration;

/// <summary>The SuperAdmin account <see cref="Data.SeedData"/> creates on first run, one per environment.</summary>
public sealed class AdminUserSeedOptions
{
    public const string SectionName = "AdminUser";

    public string Email { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    /// <summary>In the vault as ADMINUSER__PASSWORD, one value per environment.</summary>
    public string Password { get; set; } = string.Empty;
}
