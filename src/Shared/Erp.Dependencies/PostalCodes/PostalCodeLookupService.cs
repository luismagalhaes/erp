using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Erp.Dependencies.PostalCodes;

/// <summary>
/// Calls the free geoapi.pt "código postal" endpoint (json.geoapi.pt/cp/{codigo}). No API key is
/// required; the base address is set once in <see cref="DependencyInjection"/>.
/// </summary>
public sealed partial class PostalCodeLookupService(HttpClient httpClient) : IPostalCodeLookupService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

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

        // geoapi.pt returns either a single object or an array of matches depending on the code's
        // granularity; both shapes are handled so a caller never has to know which one arrived.
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(payload))
            return null;

        var entry = payload.TrimStart().StartsWith('[')
            ? JsonSerializer.Deserialize<GeoApiPostalCodeEntry[]>(payload, JsonOptions)?.FirstOrDefault()
            : JsonSerializer.Deserialize<GeoApiPostalCodeEntry>(payload, JsonOptions);

        if (entry is null)
            return null;

        return new PostalCodeLookupResult(
            entry.Cp ?? normalized,
            entry.Localidade,
            entry.Concelho,
            entry.Distrito,
            entry.Freguesia);
    }

    [GeneratedRegex(@"^\d{4}-\d{3}$")]
    private static partial Regex PortugalPostalCodePattern();
}
