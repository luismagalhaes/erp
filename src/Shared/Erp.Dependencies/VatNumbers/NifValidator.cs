namespace Erp.Dependencies.VatNumbers;

/// <summary>
/// Local, offline validation of a Portuguese NIF/NIPC: shape (9 digits), a known first-digit
/// prefix, and the module-11 check digit. This never reaches the network, so it is the first
/// gate before anyone bothers calling VIES.
/// </summary>
public static class NifValidator
{
    // Two-digit prefixes reserved for specific entity kinds; checked before the single-digit set
    // because they narrow a first digit ('7' or '9') that is otherwise not valid on its own.
    private static readonly HashSet<string> ValidTwoDigitPrefixes =
        ["45", "70", "71", "72", "74", "75", "77", "78", "79", "90", "91", "98", "99"];

    private static readonly HashSet<char> ValidSingleDigitPrefixes = ['1', '2', '3', '5', '6', '8'];

    /// <summary>True when the NIF has 9 digits, a valid entity prefix and a matching check digit.</summary>
    public static bool IsValid(string? nif) =>
        TryNormalize(nif, out var normalized)
        && HasValidPrefix(normalized)
        && HasValidCheckDigit(normalized);

    /// <summary>
    /// True when the prefix identifies a company/collective entity rather than an individual
    /// (e.g. '5' for sociedades) — the only case worth a VIES lookup.
    /// </summary>
    public static bool IsCompanyPrefix(string? nif)
    {
        if (!TryNormalize(nif, out var normalized))
            return false;

        return normalized[0] is '5' or '6' or '9'
            || ValidTwoDigitPrefixes.Contains(normalized[..2]) && normalized[0] is '7' or '9';
    }

    /// <summary>Strips whitespace and an optional "PT" country prefix, keeping only the 9 digits.</summary>
    public static bool TryNormalize(string? nif, out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(nif))
            return false;

        var candidate = nif.Trim().Replace(" ", string.Empty);
        if (candidate.StartsWith("PT", StringComparison.OrdinalIgnoreCase))
            candidate = candidate[2..];

        if (candidate.Length != 9 || !candidate.All(char.IsAsciiDigit))
            return false;

        normalized = candidate;
        return true;
    }

    private static bool HasValidPrefix(string nif) =>
        ValidTwoDigitPrefixes.Contains(nif[..2]) || ValidSingleDigitPrefixes.Contains(nif[0]);

    private static bool HasValidCheckDigit(string nif)
    {
        var sum = 0;
        for (var i = 0; i < 8; i++)
            sum += (nif[i] - '0') * (9 - i);

        var remainder = sum % 11;
        var checkDigit = remainder < 2 ? 0 : 11 - remainder;

        return checkDigit == nif[8] - '0';
    }
}
