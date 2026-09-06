namespace Erp.Sales.Application.Configuration;

/// <summary>
/// Settings of the certified billing program. The private key is deliberately not part of
/// appsettings: supply it through user secrets or a secret store.
/// </summary>
public sealed class FiscalOptions
{
    public const string SectionName = "Fiscal";

    /// <summary>Tax id of the issuer, field A of the QR code.</summary>
    public string IssuerTaxId { get; set; } = string.Empty;

    /// <summary>Certificate number assigned by the tax authority, field R of the QR code.</summary>
    public string CertificateNumber { get; set; } = "0000";

    /// <summary>Version of the signing key, stored on each document as HashControl.</summary>
    public string KeyVersion { get; set; } = "1";

    /// <summary>Producer RSA private key in PEM format. Never set this in appsettings.</summary>
    public string? PrivateKeyPem { get; set; }

    /// <summary>
    /// When no key is configured, allows generating a local development key instead of failing.
    /// The host sets this from the environment; it must stay false outside development.
    /// </summary>
    public bool AllowDevelopmentKeyGeneration { get; set; }
}
