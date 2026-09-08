using System.Text.Json.Serialization;

namespace Erp.Api.Services;

/// <summary>
/// The OData collection envelope returned by the query routes. The controllers apply the query
/// options by hand, so the envelope is written here instead of being produced by [EnableQuery].
/// </summary>
public sealed class ODataCollection<T>(int count, IReadOnlyList<T> value)
{
    [JsonPropertyName("@odata.count")]
    public int Count { get; } = count;

    [JsonPropertyName("value")]
    public IReadOnlyList<T> Value { get; } = value;
}
