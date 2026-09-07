using System.Security.Cryptography;
using Erp.FiscalPT.Signing;
using FluentAssertions;

namespace Erp.FiscalPT.Tests;

public class RsaDocumentSignerTests
{
    private const string SignatureString = "2026-01-15;2026-01-15T10:32:04;FT A2026/2;250.00;";

    [Fact]
    public void Sign_produces_a_signature_the_matching_public_key_verifies()
    {
        using var key = RSA.Create(1024);

        var hash = RsaDocumentSigner.Sign(SignatureString, key);

        RsaDocumentSigner.Verify(SignatureString, hash, key).Should().BeTrue();
    }

    [Fact]
    public void Verify_fails_when_any_signed_field_changed()
    {
        using var key = RSA.Create(1024);
        var hash = RsaDocumentSigner.Sign(SignatureString, key);

        var tampered = SignatureString.Replace("250.00", "150.00", StringComparison.Ordinal);

        RsaDocumentSigner.Verify(tampered, hash, key).Should().BeFalse();
    }

    [Fact]
    public void Verify_fails_for_a_signature_from_another_key()
    {
        using var signingKey = RSA.Create(1024);
        using var otherKey = RSA.Create(1024);

        var hash = RsaDocumentSigner.Sign(SignatureString, signingKey);

        RsaDocumentSigner.Verify(SignatureString, hash, otherKey).Should().BeFalse();
    }

    [Fact]
    public void Sign_with_a_1024_bit_key_produces_a_172_character_base64_signature()
    {
        using var key = RSA.Create(1024);

        var hash = RsaDocumentSigner.Sign(SignatureString, key);

        hash.Should().HaveLength(172);
    }

    [Fact]
    public void ExtractPrintableHash_takes_positions_1_11_21_and_31()
    {
        // 40 characters, so the sampled positions are easy to read off.
        const string hash = "ABCDEFGHIJ" + "KLMNOPQRST" + "UVWXYZabcd" + "efghijklmn";

        // The four characters run together: on the printed document the hyphen goes between them
        // and the "Processado por programa certificado" notice, never between the characters.
        RsaDocumentSigner.ExtractPrintableHash(hash).Should().Be("AKUe");
    }

    [Fact]
    public void ExtractPrintableHash_never_puts_a_separator_between_the_characters()
    {
        const string hash = "ABCDEFGHIJ" + "KLMNOPQRST" + "UVWXYZabcd" + "efghijklmn";

        var characters = RsaDocumentSigner.ExtractPrintableHash(hash);

        characters.Should().HaveLength(4);
        characters.Should().NotContain("-");
    }

    [Fact]
    public void ExtractPrintableHash_rejects_a_hash_shorter_than_31_characters()
    {
        var act = () => RsaDocumentSigner.ExtractPrintableHash("tooshort");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_chain_of_documents_verifies_end_to_end()
    {
        using var key = RSA.Create(1024);

        var first = DocumentSignatureString.Build(
            new DateOnly(2026, 1, 1), new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc), "FT A2026/1", 100m, string.Empty);
        var firstHash = RsaDocumentSigner.Sign(first, key);

        var second = DocumentSignatureString.Build(
            new DateOnly(2026, 1, 2), new DateTime(2026, 1, 2, 9, 0, 0, DateTimeKind.Utc), "FT A2026/2", 250m, firstHash);
        var secondHash = RsaDocumentSigner.Sign(second, key);

        RsaDocumentSigner.Verify(first, firstHash, key).Should().BeTrue();
        RsaDocumentSigner.Verify(second, secondHash, key).Should().BeTrue();

        // Re-chaining the second document onto a different predecessor breaks verification.
        var reChained = DocumentSignatureString.Build(
            new DateOnly(2026, 1, 2), new DateTime(2026, 1, 2, 9, 0, 0, DateTimeKind.Utc), "FT A2026/2", 250m, "other");

        RsaDocumentSigner.Verify(reChained, secondHash, key).Should().BeFalse();
    }
}
