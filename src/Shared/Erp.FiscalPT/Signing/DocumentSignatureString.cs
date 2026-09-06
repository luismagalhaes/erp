using System.Globalization;

namespace Erp.FiscalPT.Signing;

/// <summary>
/// Builds the string signed for every fiscally relevant document, as required by
/// article 6 of Portaria 363/2010: InvoiceDate;SystemEntryDate;InvoiceNo;GrossTotal;PreviousHash.
/// Formatting is culture invariant on purpose - a decimal comma invalidates the signature.
/// </summary>
public static class DocumentSignatureString
{
    private const string DateFormat = "yyyy-MM-dd";
    private const string DateTimeFormat = "yyyy-MM-ddTHH:mm:ss";
    private const string AmountFormat = "0.00";

    public static string Build(
        DateOnly documentDate,
        DateTime systemEntryDate,
        string documentNumber,
        decimal grossTotal,
        string? previousHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentNumber);

        return string.Concat(
            documentDate.ToString(DateFormat, CultureInfo.InvariantCulture),
            ";",
            systemEntryDate.ToString(DateTimeFormat, CultureInfo.InvariantCulture),
            ";",
            documentNumber,
            ";",
            grossTotal.ToString(AmountFormat, CultureInfo.InvariantCulture),
            ";",
            previousHash ?? string.Empty);
    }
}
