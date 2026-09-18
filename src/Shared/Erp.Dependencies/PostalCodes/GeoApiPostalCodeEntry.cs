using System.Text.Json.Serialization;

namespace Erp.Dependencies.PostalCodes;

/// <summary>
/// Raw shape of a geoapi.pt "/cp/{codigo}" entry. Only the fields the ERP actually uses are
/// mapped; the service exposes <see cref="PostalCodeLookupResult"/> instead of this DTO.
/// </summary>
internal sealed class GeoApiPostalCodeEntry
{
    [JsonPropertyName("CP")]
    public string? Cp { get; set; }

    [JsonPropertyName("Localidade")]
    public string? Localidade { get; set; }

    [JsonPropertyName("Concelho")]
    public string? Concelho { get; set; }

    [JsonPropertyName("Distrito")]
    public string? Distrito { get; set; }

    [JsonPropertyName("Freguesia")]
    public string? Freguesia { get; set; }
}
