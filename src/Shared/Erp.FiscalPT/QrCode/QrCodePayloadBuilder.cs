using System.Globalization;
using System.Text;
using Erp.FiscalPT.Documents;

namespace Erp.FiscalPT.QrCode;

/// <summary>
/// Builds the QR code message defined by Portaria 195/2020: "Field:Value" pairs joined by "*".
/// Only applicable fields are written; empty amounts are omitted.
/// </summary>
public static class QrCodePayloadBuilder
{
    private const char FieldSeparator = '*';
    private const string AmountFormat = "0.00";
    private const string DateFormat = "yyyyMMdd";

    /// <summary>Field letter that carries the taxable bases of each fiscal space.</summary>
    private static readonly Dictionary<string, char> RegionFieldLetters = new(StringComparer.Ordinal)
    {
        [TaxCountryRegions.Mainland] = 'I',
        [TaxCountryRegions.Azores] = 'J',
        [TaxCountryRegions.Madeira] = 'K'
    };

    /// <summary>Index of the taxable base within the region block; the VAT amount goes in the next one.</summary>
    private static readonly Dictionary<string, int> TaxCodeBaseIndexes = new(StringComparer.Ordinal)
    {
        [TaxCodes.Reduced] = 3,
        [TaxCodes.Intermediate] = 5,
        [TaxCodes.Normal] = 7
    };

    public static string Build(QrCodeFields fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        var builder = new StringBuilder();

        Append(builder, "A", fields.IssuerTaxId);
        Append(builder, "B", fields.BuyerTaxId);
        Append(builder, "C", fields.BuyerCountry);
        Append(builder, "D", fields.DocumentType);
        Append(builder, "E", fields.DocumentStatus);
        Append(builder, "F", fields.DocumentDate.ToString(DateFormat, CultureInfo.InvariantCulture));
        Append(builder, "G", fields.DocumentNumber);
        Append(builder, "H", fields.Atcud);

        AppendTaxAmounts(builder, fields.TaxAmounts);

        Append(builder, "N", Format(fields.TotalTaxes));
        Append(builder, "O", Format(fields.GrossTotal));
        Append(builder, "Q", fields.HashCharacters);
        Append(builder, "R", fields.CertificateNumber);

        if (!string.IsNullOrWhiteSpace(fields.OtherInformation))
            Append(builder, "S", fields.OtherInformation);

        return builder.ToString();
    }

    private static void AppendTaxAmounts(StringBuilder builder, IReadOnlyList<QrCodeTaxAmount> taxAmounts)
    {
        foreach (var region in taxAmounts.Select(x => x.TaxCountryRegion).Distinct(StringComparer.Ordinal))
        {
            if (!RegionFieldLetters.TryGetValue(region, out var letter))
                throw new ArgumentException($"Unknown tax country region '{region}'.", nameof(taxAmounts));

            Append(builder, $"{letter}1", region);

            var exemptBase = taxAmounts
                .Where(x => string.Equals(x.TaxCountryRegion, region, StringComparison.Ordinal)
                            && string.Equals(x.TaxCode, TaxCodes.Exempt, StringComparison.Ordinal))
                .Sum(x => x.TaxableBase);

            if (exemptBase > 0)
                Append(builder, $"{letter}2", Format(exemptBase));

            foreach (var (taxCode, baseIndex) in TaxCodeBaseIndexes)
            {
                var entries = taxAmounts
                    .Where(x => string.Equals(x.TaxCountryRegion, region, StringComparison.Ordinal)
                                && string.Equals(x.TaxCode, taxCode, StringComparison.Ordinal))
                    .ToList();

                if (entries.Count == 0)
                    continue;

                Append(builder, $"{letter}{baseIndex}", Format(entries.Sum(x => x.TaxableBase)));
                Append(builder, $"{letter}{baseIndex + 1}", Format(entries.Sum(x => x.TaxAmount)));
            }
        }
    }

    private static void Append(StringBuilder builder, string field, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (builder.Length > 0)
            builder.Append(FieldSeparator);

        builder.Append(field).Append(':').Append(value);
    }

    private static string Format(decimal value) =>
        value.ToString(AmountFormat, CultureInfo.InvariantCulture);
}
