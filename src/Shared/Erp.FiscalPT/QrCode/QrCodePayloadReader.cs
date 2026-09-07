namespace Erp.FiscalPT.QrCode;

/// <summary>
/// Reads back a field of a QR code message. The printed document needs the certificate number
/// (field R) of the program that actually issued the document, and that is what travels in the
/// payload — reading it from there beats re-reading today's configuration, which may have moved on.
/// </summary>
public static class QrCodePayloadReader
{
    private const char FieldSeparator = '*';
    private const char ValueSeparator = ':';

    /// <summary>Value of a field, or null when the payload does not carry it.</summary>
    public static string? GetField(string? payload, string field)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);

        if (string.IsNullOrWhiteSpace(payload))
            return null;

        foreach (var pair in payload.Split(FieldSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf(ValueSeparator);
            if (separator <= 0)
                continue;

            if (pair.AsSpan(0, separator).SequenceEqual(field))
                return pair[(separator + 1)..];
        }

        return null;
    }

    /// <summary>Certificate number assigned by the tax authority, field R.</summary>
    public static string? GetCertificateNumber(string? payload) => GetField(payload, "R");
}
