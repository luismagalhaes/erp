using System.Globalization;

namespace Erp.FiscalPT.Documents;

/// <summary>
/// Document identifier in the SAF-T InvoiceNo format: document type, series and sequence,
/// for example "FT A2026/2". This is also the value signed and shown in the QR code field G.
/// </summary>
public static class DocumentNumber
{
    public static string Build(string documentType, string seriesCode, int sequenceNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(seriesCode);
        ArgumentOutOfRangeException.ThrowIfLessThan(sequenceNumber, 1);

        return string.Concat(
            documentType,
            " ",
            seriesCode,
            "/",
            sequenceNumber.ToString(CultureInfo.InvariantCulture));
    }
}
