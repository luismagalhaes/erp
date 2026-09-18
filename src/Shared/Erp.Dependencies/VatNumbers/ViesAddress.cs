using System.Text.RegularExpressions;

namespace Erp.Dependencies.VatNumbers;

/// <summary>
/// VIES answers a Portuguese company's address as one block of free text — the street address, a
/// blank line, then the postal code and the locality together on the one last line ("4715-293
/// BRAGA"), not each on a line of its own. Splitting it here is what lets a caller fill three
/// separate fields instead of one, which is what happened before this existed: the raw block sat in
/// the address field alone.
/// </summary>
public static partial class ViesAddress
{
    public static (string? Address, string? PostalCode, string? City) Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return (null, null, null);

        var lines = raw.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length == 0)
            return (null, null, null);

        // The usual shape: postal code and locality share the last line, space separated —
        // "4715-293 BRAGA" — so whatever is left of that line once the code itself is removed is
        // the locality, and every earlier line is the address.
        var lastLine = lines[^1];
        var lastLineMatch = PostalCodePattern().Match(lastLine);

        if (lastLineMatch.Success)
        {
            // Only what comes after the code: VIES writes "code locality", never the other way
            // round, so anything before it — a stray country prefix, or nothing at all — is never
            // part of the locality.
            var city = lastLine[(lastLineMatch.Index + lastLineMatch.Length)..].TrimStart(' ', '-', ',').Trim();
            var address = string.Join(", ", lines[..^1]);

            return (NullIfBlank(address), Rebuild(lastLineMatch), NullIfBlank(city));
        }

        // A postal code on a line of its own, followed by the locality on the next — accepted too,
        // in case that shape turns up for some other entry.
        if (lines.Length >= 2)
        {
            var secondToLastMatch = PostalCodePattern().Match(lines[^2]);

            if (secondToLastMatch.Success)
            {
                var address = string.Join(", ", lines[..^2]);
                return (NullIfBlank(address), Rebuild(secondToLastMatch), NullIfBlank(lastLine));
            }
        }

        // Neither shape matched — nothing worth guessing at, so the whole block stays the address.
        return (Normalize(raw), null, null);
    }

    // Searched, not matched whole: VIES is not guaranteed to send the code as a bare "0000-000" — a
    // stray country prefix ("PT-0000-000") or a space instead of the dash is still a postal code
    // worth finding, so long as the four-then-three digit shape is in there somewhere. Rebuilding it
    // as "0000-000" regardless of how it arrived is what the postal code field this feeds into
    // actually requires.
    private static string Rebuild(Match match) => $"{match.Groups[1].Value}-{match.Groups[2].Value}";

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Normalize(string raw)
    {
        var normalized = raw.Replace('\n', ' ').Trim();
        return normalized.Length == 0 ? null : normalized;
    }

    [GeneratedRegex(@"(\d{4})[\s-]?(\d{3})")]
    private static partial Regex PostalCodePattern();
}
