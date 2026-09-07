using Erp.FiscalPT.QrCode;
using FluentAssertions;

namespace Erp.FiscalPT.Tests;

public class QrCodePayloadReaderTests
{
    private const string Payload =
        "A:123456789*B:999999990*C:PT*D:FT*E:N*F:20260115*G:FT A2026/1*H:JFTX7RK9-1*I1:PT*I7:200.00*I8:46.00*N:46.00*O:246.00*Q:kR9x*R:9999";

    [Theory]
    [InlineData("A", "123456789")]
    [InlineData("D", "FT")]
    [InlineData("H", "JFTX7RK9-1")]
    [InlineData("Q", "kR9x")]
    [InlineData("R", "9999")]
    public void GetField_reads_the_value_of_a_field(string field, string expected)
    {
        QrCodePayloadReader.GetField(Payload, field).Should().Be(expected);
    }

    /// <summary>The document number carries a space and a slash, which must survive intact.</summary>
    [Fact]
    public void GetField_keeps_a_value_with_punctuation()
    {
        QrCodePayloadReader.GetField(Payload, "G").Should().Be("FT A2026/1");
    }

    /// <summary>"I1" must not be found when asking for "I", nor the other way round.</summary>
    [Fact]
    public void GetField_matches_the_whole_field_name()
    {
        QrCodePayloadReader.GetField(Payload, "I").Should().BeNull();
        QrCodePayloadReader.GetField(Payload, "I1").Should().Be("PT");
    }

    [Fact]
    public void GetField_returns_null_for_a_field_that_is_not_there()
    {
        QrCodePayloadReader.GetField(Payload, "S").Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetField_returns_null_for_an_empty_payload(string? payload)
    {
        QrCodePayloadReader.GetField(payload, "A").Should().BeNull();
    }

    [Fact]
    public void GetCertificateNumber_reads_field_R()
    {
        QrCodePayloadReader.GetCertificateNumber(Payload).Should().Be("9999");
    }

    /// <summary>
    /// The printed mention takes the certificate number from the document itself, so a change in
    /// configuration cannot rewrite what an already issued document says.
    /// </summary>
    [Fact]
    public void GetCertificateNumber_follows_the_document_not_the_configuration()
    {
        var older = Payload.Replace("R:9999", "R:1234", StringComparison.Ordinal);

        QrCodePayloadReader.GetCertificateNumber(older).Should().Be("1234");
    }
}
