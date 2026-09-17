namespace Erp.FiscalPT;

/// <summary>
/// Settings for the AT (Autoridade Tributária) webservices — a different credential than
/// <see cref="FiscalOptions.SigningKeyPem"/>, which signs documents locally and never talks to AT.
/// <see cref="FiscalOptions.SigningKeyPem"/> is still grouped with these in the vault, under
/// <see cref="SectionName"/>, because it is AT-mandated the same way the others are.
/// </summary>
public sealed class AtOptions
{
    public const string SectionName = "AT";

    /// <summary>Base address of the "Comunicação de Séries Documentais" webservice (SeriesWSService).</summary>
    public string SeriesUrl { get; set; } = string.Empty;

    /// <summary>Base address of the "Comunicação dos Documentos de Transporte" webservice — a different endpoint than <see cref="SeriesUrl"/>.</summary>
    public string TransportDocumentsUrl { get; set; } = string.Empty;

    /// <summary>
    /// Client certificate (PFX/P12) used to authenticate the connection itself against the AT
    /// webservices — it is <em>our</em> identity, not AT's. Base64 encoded, because the PFX is
    /// binary and the vault only holds text. Never set this in appsettings — it's in the vault as
    /// AT__CLIENTCERTIFICATEBASE64, one value per environment.
    /// </summary>
    public string? ClientCertificateBase64 { get; set; }

    /// <summary>In the vault as AT__CLIENTCERTIFICATEPASSWORD, one value per environment.</summary>
    public string? ClientCertificatePassword { get; set; }

    /// <summary>
    /// AT's own RSA public key (PEM) — <em>their</em> key, not ours — used to encrypt the
    /// WS-Security symmetric key on every call. Obtained from the AT (by email to asi-cd@at.gov.pt,
    /// or from the "Testar Webservice" page). Already PEM text, so it goes into the vault as-is, no
    /// base64 step. Never set this in appsettings — it's in the vault as AT__PUBLICKEYPEM, one
    /// value per environment.
    /// </summary>
    public string? PublicKeyPem { get; set; }
}
