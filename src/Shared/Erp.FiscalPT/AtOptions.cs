namespace Erp.FiscalPT;

/// <summary>
/// Settings for the AT (Autoridade Tributária) webservices — a different credential than
/// <see cref="FiscalOptions.PrivateKeyPem"/>, which signs documents locally and never talks to AT.
/// </summary>
public sealed class AtOptions
{
    public const string SectionName = "AT";

    public string WebserviceUrl { get; set; } = string.Empty;

    public string AtcudUrl { get; set; } = string.Empty;

    /// <summary>
    /// Client certificate (PFX/P12) used to authenticate against the AT webservices, base64
    /// encoded. Never set this in appsettings — it's in the vault as AT__CERTIFICATEBASE64, one
    /// value per environment.
    /// </summary>
    public string? CertificateBase64 { get; set; }

    /// <summary>In the vault as AT__CERTIFICATEPASSWORD, one value per environment.</summary>
    public string? CertificatePassword { get; set; }
}
