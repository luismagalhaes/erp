using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Erp.FiscalPT.AtWebservice.Crypto;
using FluentAssertions;

namespace Erp.FiscalPT.Tests.AtWebservice;

public class WsSecurityHeaderBuilderTests
{
    [Fact]
    public void Build_produces_fields_the_matching_private_key_can_decrypt()
    {
        using var rsa = RSA.Create(2048);
        var publicKeyPem = rsa.ExportSubjectPublicKeyInfoPem();
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var header = WsSecurityHeaderBuilder.Build("500123456/1", "s3cr3t", publicKeyPem, now);

        header.Username.Should().Be("500123456/1");

        var symmetricKey = rsa.Decrypt(Convert.FromBase64String(header.Nonce), RSAEncryptionPadding.Pkcs1);
        symmetricKey.Should().HaveCount(16, "the manual specifies a 128 bit AES key");

        DecryptAes(symmetricKey, header.Password).Should().Be("s3cr3t");
        DecryptAes(symmetricKey, header.Created).Should().StartWith("2026-01-01T12:00:00");
    }

    [Fact]
    public void Build_uses_a_fresh_symmetric_key_every_call()
    {
        using var rsa = RSA.Create(2048);
        var publicKeyPem = rsa.ExportSubjectPublicKeyInfoPem();

        var first = WsSecurityHeaderBuilder.Build("500123456/1", "s3cr3t", publicKeyPem, DateTime.UtcNow);
        var second = WsSecurityHeaderBuilder.Build("500123456/1", "s3cr3t", publicKeyPem, DateTime.UtcNow);

        // AT rejects a repeated Nonce (error 13); reusing one is exactly what must never happen.
        first.Nonce.Should().NotBe(second.Nonce);
    }

    [Fact]
    public void Build_accepts_a_certificate_pem_as_well_as_a_bare_public_key()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=AT Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var certificatePem = certificate.ExportCertificatePem();

        var header = WsSecurityHeaderBuilder.Build("500123456/1", "s3cr3t", certificatePem, DateTime.UtcNow);

        header.Nonce.Should().NotBeNullOrWhiteSpace();
    }

    private static string DecryptAes(byte[] key, string base64CipherText)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var cipherBytes = Convert.FromBase64String(base64CipherText);
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }
}
