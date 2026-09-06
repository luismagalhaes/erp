using System.Globalization;

namespace Erp.FiscalPT.Documents;

/// <summary>
/// Unique document code required by Portaria 195/2020, built from the validation code the
/// tax authority returns when a series is communicated, plus the document sequence number.
/// </summary>
public static class Atcud
{
    public const string Prefix = "ATCUD:";

    public static string Build(string validationCode, int sequenceNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(validationCode);
        ArgumentOutOfRangeException.ThrowIfLessThan(sequenceNumber, 1);

        return string.Concat(
            validationCode,
            "-",
            sequenceNumber.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>The ATCUD as printed on the document, including the mandatory prefix.</summary>
    public static string BuildPrintable(string validationCode, int sequenceNumber) =>
        Prefix + Build(validationCode, sequenceNumber);
}
