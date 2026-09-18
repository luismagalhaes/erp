using System.Text.Json.Serialization;

namespace Erp.Dependencies.VatNumbers;

/// <summary>Raw shape of a VIES REST API "/ms/{countryCode}/vat/{vatNumber}" response.</summary>
internal sealed class ViesVatResponse
{
    [JsonPropertyName("isValid")]
    public bool IsValid { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("userError")]
    public string? UserError { get; set; }
}
