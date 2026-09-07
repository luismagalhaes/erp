using QRCoder;

namespace Erp.FiscalPT.QrCode;

/// <summary>
/// Renders the QR code message as a PNG. Uses QRCoder's byte renderer rather than the bitmap one,
/// so it works the same on Windows, Linux and containers without any drawing library.
/// </summary>
public static class QrCodeImage
{
    /// <summary>
    /// Error correction level. Portaria 195/2020 sets the minimum at M, and the payload is short
    /// enough that M keeps the code readable when printed small.
    /// </summary>
    private const QRCodeGenerator.ECCLevel CorrectionLevel = QRCodeGenerator.ECCLevel.M;

    /// <summary>Renders the payload as PNG bytes.</summary>
    /// <param name="pixelsPerModule">Size of each square of the code, in pixels.</param>
    public static byte[] RenderPng(string payload, int pixelsPerModule = 6)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        ArgumentOutOfRangeException.ThrowIfLessThan(pixelsPerModule, 1);

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, CorrectionLevel);

        return new PngByteQRCode(data).GetGraphic(pixelsPerModule);
    }

    /// <summary>
    /// Renders the payload as a <c>data:</c> URI, ready to drop into an <c>img</c> tag on a
    /// printable document.
    /// </summary>
    public static string RenderDataUri(string payload, int pixelsPerModule = 6) =>
        $"data:image/png;base64,{Convert.ToBase64String(RenderPng(payload, pixelsPerModule))}";
}
