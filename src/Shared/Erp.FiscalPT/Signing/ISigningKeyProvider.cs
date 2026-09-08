using System.Security.Cryptography;

namespace Erp.FiscalPT.Signing;

/// <summary>
/// Supplies the software producer RSA key. The private key never lives in the repository or
/// in appsettings - it comes from user secrets in development and from a secret store in
/// production.
/// </summary>
public interface ISigningKeyProvider
{
    /// <summary>Version of the current key, stored on each document as HashControl.</summary>
    string KeyVersion { get; }

    RSA GetPrivateKey();

    RSA GetPublicKey();
}
