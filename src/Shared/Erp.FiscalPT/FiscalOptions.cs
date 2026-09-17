namespace Erp.FiscalPT;

/// <summary>
/// Settings of the certified program: who produced it, and with what key it signs. The private key
/// is deliberately not part of appsettings — supply it through user secrets or a secret store.
/// </summary>
/// <remarks>
/// One type for every module that issues fiscal documents. Sales and Purchasing sign with the same
/// key and print the same certificate number, because it is the same program that was certified.
/// </remarks>
public sealed class FiscalOptions
{
    public const string SectionName = "Fiscal";

    /// <summary>Tax id of the issuer, field A of the QR code.</summary>
    public string IssuerTaxId { get; set; } = string.Empty;

    /// <summary>Certificate number assigned by the tax authority, field R of the QR code.</summary>
    public string CertificateNumber { get; set; } = "0000";

    /// <summary>Version of the signing key, stored on each document as HashControl.</summary>
    public string KeyVersion { get; set; } = "1";

    /// <summary>
    /// Producer RSA private key in PEM format, used to sign documents locally (SAF-T hash chain).
    /// Never set this in appsettings — it's in the vault as AT__SIGNINGKEYPEM (grouped with the
    /// other AT-related credentials, not under Fiscal, even though it lives on this options type),
    /// one value per environment. See <see cref="DependencyInjection.AddFiscalPT"/> for the binding.
    /// </summary>
    public string? SigningKeyPem { get; set; }

    /// <summary>
    /// When no key is configured, allows generating a local development key instead of failing.
    /// The host sets this from the environment; it must stay false outside development.
    /// </summary>
    public bool AllowDevelopmentKeyGeneration { get; set; }

    /// <summary>"ProductName/CompanyName" in the SAF-T header, in the form that was registered.</summary>
    public string ProductId { get; set; } = "ErpPortugal/ErpPortugal";

    public string ProductVersion { get; set; } = "1.0";

    /// <summary>The producer half of these settings, as the SAF-T header needs it.</summary>
    public Saft.SaftProducerInfo ToProducerInfo() =>
        new(IssuerTaxId, CertificateNumber, ProductId, ProductVersion);
}
