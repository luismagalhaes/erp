using Erp.FiscalPT.QrCode;
using FluentAssertions;

namespace Erp.FiscalPT.Tests;

public class QrCodeImageTests
{
    private const string Payload =
        "A:123456789*B:999999990*C:PT*D:FT*E:N*F:20260115*G:FT A2026/1*H:JFTX7RK9-1*I1:PT*I7:200.00*I8:46.00*N:46.00*O:246.00*Q:kR9x*R:9999";

    /// <summary>The first eight bytes of every PNG, so this proves it is really an image.</summary>
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public void RenderPng_produces_a_png()
    {
        var image = QrCodeImage.RenderPng(Payload);

        image.Should().NotBeEmpty();
        image.Take(8).Should().Equal(PngSignature);
    }

    [Fact]
    public void RenderPng_grows_with_the_module_size()
    {
        var small = QrCodeImage.RenderPng(Payload, pixelsPerModule: 2);
        var large = QrCodeImage.RenderPng(Payload, pixelsPerModule: 10);

        large.Length.Should().BeGreaterThan(small.Length);
    }

    [Fact]
    public void RenderPng_is_deterministic()
    {
        // The same document must always print the same code.
        QrCodeImage.RenderPng(Payload).Should().Equal(QrCodeImage.RenderPng(Payload));
    }

    [Fact]
    public void RenderDataUri_is_ready_for_an_img_tag()
    {
        var uri = QrCodeImage.RenderDataUri(Payload);

        uri.Should().StartWith("data:image/png;base64,");
        Convert.FromBase64String(uri["data:image/png;base64,".Length..]).Take(8).Should().Equal(PngSignature);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RenderPng_rejects_an_empty_payload(string payload)
    {
        var act = () => QrCodeImage.RenderPng(payload);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RenderPng_rejects_a_module_size_below_one()
    {
        var act = () => QrCodeImage.RenderPng(Payload, pixelsPerModule: 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
