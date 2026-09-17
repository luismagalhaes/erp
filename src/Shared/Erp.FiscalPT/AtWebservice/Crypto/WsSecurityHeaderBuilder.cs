using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Erp.FiscalPT.AtWebservice.Crypto;

/// <summary>
/// Builds the WS-Security UsernameToken the AT SOAP webservices require: a fresh AES-128 key per
/// request, encrypted with AT's own RSA public key (the Nonce field), used in turn to encrypt the
/// password and the request timestamp. Follows the exact construction described in the AT
/// integration manual ("Comunicação dos Documentos de Transporte", section 4.1) — RSA/PKCS1 for the
/// symmetric key and AES/ECB/PKCS5(7)Padding for the password and timestamp, since the manual gives
/// the formulas but not explicit padding names, and RSA/PKCS1 + AES/ECB/PKCS5 is the convention
/// documented by other integrators against this same AT webservice family.
/// </summary>
public static class WsSecurityHeaderBuilder
{
    private const int SymmetricKeySizeBytes = 16; // 128 bits, per the manual.

    public static WsSecurityHeader Build(
        string username,
        string password,
        string authenticationPublicKeyPem,
        DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentException.ThrowIfNullOrWhiteSpace(authenticationPublicKeyPem);

        var symmetricKey = RandomNumberGenerator.GetBytes(SymmetricKeySizeBytes);

        using var publicKey = ImportPublicKey(authenticationPublicKeyPem);

        // AT's own scheme, not a choice made here — see the class doc comment.
#pragma warning disable S5542 // Encryption algorithms should be used with secure mode and padding scheme
        var nonce = publicKey.Encrypt(symmetricKey, RSAEncryptionPadding.Pkcs1);
#pragma warning restore S5542

        var timestamp = utcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

        return new WsSecurityHeader(
            Username: username,
            Password: Convert.ToBase64String(EncryptWithAes(symmetricKey, password)),
            Nonce: Convert.ToBase64String(nonce),
            Created: Convert.ToBase64String(EncryptWithAes(symmetricKey, timestamp)));
    }

    private static byte[] EncryptWithAes(byte[] key, string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.PKCS7; // PKCS5 and PKCS7 are the same for AES's 16-byte block size.

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        return encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
    }

    /// <summary>
    /// AT hands this key out by email as a PEM file; accept either a bare public key
    /// ("BEGIN PUBLIC KEY") or a certificate ("BEGIN CERTIFICATE") since which one AT actually sends
    /// is not documented in the manual itself.
    /// </summary>
    private static RSA ImportPublicKey(string pem)
    {
        if (pem.Contains("BEGIN CERTIFICATE", StringComparison.Ordinal))
        {
            using var certificate = X509CertificateLoader.LoadCertificate(Encoding.ASCII.GetBytes(pem));
            return certificate.GetRSAPublicKey()
                ?? throw new InvalidOperationException("The AT authentication certificate has no RSA public key.");
        }

        var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        return rsa;
    }
}
