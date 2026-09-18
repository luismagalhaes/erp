using System.Text.Json.Serialization;

namespace Erp.Dependencies.PostalCodes;

/// <summary>
/// Raw shape of a moradas.dev "/cp/{cp7}" entry. Only the fields the ERP actually uses are mapped;
/// the service exposes <see cref="PostalCodeLookupResult"/> instead of this DTO.
/// </summary>
internal sealed class MoradasPostalCodeEntry
{
    [JsonPropertyName("cp7")]
    public string? Cp7 { get; set; }

    [JsonPropertyName("localidade")]
    public string? Localidade { get; set; }

    [JsonPropertyName("concelho")]
    public string? Concelho { get; set; }

    [JsonPropertyName("distrito")]
    public string? Distrito { get; set; }

    [JsonPropertyName("arterias")]
    public List<MoradasArteria>? Arterias { get; set; }
}

/// <summary>One street moradas.dev lists for a postal code, as raw as it comes back.</summary>
internal sealed class MoradasArteria
{
    [JsonPropertyName("street")]
    public string? Street { get; set; }

    [JsonPropertyName("troco")]
    public string? Troco { get; set; }
}
