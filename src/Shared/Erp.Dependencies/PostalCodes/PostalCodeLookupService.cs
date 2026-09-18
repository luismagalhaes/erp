using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Linq;

namespace Erp.Dependencies.PostalCodes;

/// <summary>
/// Calls the free moradas.dev "código postal" endpoint (moradas.dev/cp/{cp7}). No API key is
/// required — a reasonable per-IP rate limit is the only restriction — and the base address is set
/// once in <see cref="DependencyInjection"/>.
/// </summary>
public sealed partial class PostalCodeLookupService(HttpClient httpClient) : IPostalCodeLookupService
{
    public async Task<PostalCodeLookupResult?> LookupAsync(string postalCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(postalCode);

        var normalized = postalCode.Trim();
        if (!PortugalPostalCodePattern().IsMatch(normalized))
            return null;

        using var response = await httpClient.GetAsync($"cp/{normalized}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
            return null;

        var entry = JsonSerializer.Deserialize<MoradasPostalCodeEntry>(body);
        if (entry is null)
            return null;

        // moradas.dev has no equivalent of geoapi.pt's freguesia (parish) — only the district,
        // municipality and locality it returns are mapped; a caller after the parish-level detail
        // has nothing here to fall back to.
        return new PostalCodeLookupResult(
            entry.Cp7 ?? normalized,
            entry.Localidade,
            entry.Concelho,
            entry.Distrito,
            Parish: null,
            Streets: MapStreets(entry.Arterias));
    }

    // moradas.dev repeats the same street verbatim when more than one door number range falls in
    // this postal code but the segments happen to be identical; Distinct() on the record's value
    // equality collapses those back down to one entry.
    private static IReadOnlyList<PostalCodeStreet> MapStreets(List<MoradasArteria>? arterias) =>
        (arterias ?? [])
            .Where(a => !string.IsNullOrWhiteSpace(a.Street))
            .Select(a => new PostalCodeStreet(a.Street!.Trim(), string.IsNullOrWhiteSpace(a.Troco) ? null : a.Troco.Trim()))
            .Distinct()
            .ToList();

    [GeneratedRegex(@"^\d{4}-\d{3}$")]
    private static partial Regex PortugalPostalCodePattern();
}
