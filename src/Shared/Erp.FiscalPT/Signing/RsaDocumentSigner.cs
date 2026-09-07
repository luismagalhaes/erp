using System.Security.Cryptography;
using System.Text;

namespace Erp.FiscalPT.Signing;

/// <summary>
/// Signs the document string with the software producer private key.
/// Portaria 363/2010 requires RSA with a SHA-1 digest, PKCS#1 v1.5 padding and a 1024 bit key.
/// </summary>
public static class RsaDocumentSigner
{
    /// <summary>Positions (1-based) of the hash characters printed on the document.</summary>
    private static readonly int[] PrintablePositions = [1, 11, 21, 31];

    public static string Sign(string signatureString, RSA privateKey)
    {
        ArgumentNullException.ThrowIfNull(privateKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(signatureString);

        var payload = Encoding.UTF8.GetBytes(signatureString);
        var signature = privateKey.SignData(payload, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);

        return Convert.ToBase64String(signature);
    }

    public static bool Verify(string signatureString, string hash, RSA publicKey)
    {
        ArgumentNullException.ThrowIfNull(publicKey);

        if (string.IsNullOrWhiteSpace(signatureString) || string.IsNullOrWhiteSpace(hash))
            return false;

        byte[] signature;
        try
        {
            signature = Convert.FromBase64String(hash);
        }
        catch (FormatException)
        {
            return false;
        }

        var payload = Encoding.UTF8.GetBytes(signatureString);
        return publicKey.VerifyData(payload, signature, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
    }

    /// <summary>
    /// The four characters taken from positions 1, 11, 21 and 31 of the Base64 signature, one
    /// after the other and with nothing between them. They go both on the printed document — where
    /// a hyphen separates them from the "Processado por programa certificado" notice — and into
    /// field Q of the QR code.
    /// </summary>
    public static string ExtractPrintableHash(string hash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);

        var maxPosition = PrintablePositions[^1];
        if (hash.Length < maxPosition)
            throw new ArgumentException($"Hash must have at least {maxPosition} characters.", nameof(hash));

        return string.Concat(PrintablePositions.Select(position => hash[position - 1]));
    }
}
