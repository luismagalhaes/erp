using System.Text.RegularExpressions;

namespace Erp.Common;

/// <summary>
/// Postal code rules. Only Portugal has a fixed shape here — "XXXX-XXX", digits only — and it is the
/// one the SAF-T and the AT webservices expect; codes from other countries are taken as written.
/// </summary>
public static partial class PostalCodes
{
    public const string PortugalCountryCode = "PT";

    /// <summary>The input mask the screens use: 0 stands for a digit.</summary>
    public const string PortugalMask = "0000-000";

    public static bool IsPortugal(string? country) =>
        string.IsNullOrWhiteSpace(country)
        || string.Equals(country.Trim(), PortugalCountryCode, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True when the code is acceptable for the country. An empty code is always acceptable: whether
    /// one is required is the caller's rule, not the format's.
    /// </summary>
    public static bool IsValid(string? postalCode, string? country) =>
        string.IsNullOrWhiteSpace(postalCode)
        || !IsPortugal(country)
        || PortugalPattern().IsMatch(postalCode.Trim());

    [GeneratedRegex(@"^\d{4}-\d{3}$")]
    private static partial Regex PortugalPattern();
}
