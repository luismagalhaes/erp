using System.Text;
using System.Text.Json.Serialization;

namespace Erp.Main.Services;

/// <summary>
/// The shape of an OData collection response with $count requested. Only the two members the
/// grids need are mapped; everything else in the payload is ignored.
/// </summary>
public sealed class ODataResponse<T>
{
    [JsonPropertyName("value")]
    public List<T> Value { get; set; } = [];

    [JsonPropertyName("@odata.count")]
    public int Count { get; set; }
}

/// <summary>
/// Builds the query string sent to an [EnableQuery] endpoint. Kept separate from the API clients
/// so every server driven grid produces the same options.
/// </summary>
public sealed class ODataQuery
{
    private readonly List<string> _filters = [];
    private readonly List<string> _orderBy = [];

    public int? Skip { get; set; }

    public int? Top { get; set; }

    public bool Count { get; set; } = true;

    public ODataQuery Filter(string? expression)
    {
        if (!string.IsNullOrWhiteSpace(expression))
            _filters.Add(expression);

        return this;
    }

    public ODataQuery OrderBy(string field, bool descending) =>
        OrderBy($"{field} {(descending ? "desc" : "asc")}");

    public ODataQuery OrderBy(string? expression)
    {
        if (!string.IsNullOrWhiteSpace(expression))
            _orderBy.Add(expression);

        return this;
    }

    /// <summary>Escapes a value so it can be embedded in an OData string literal.</summary>
    public static string Literal(string value) => $"'{value.Replace("'", "''")}'";

    /// <summary>Builds a case insensitive contains over several fields, joined by or.</summary>
    public static string ContainsAny(string term, params string[] fields)
    {
        var literal = Literal(term.ToLowerInvariant());
        return string.Join(" or ", fields.Select(field => $"contains(tolower({field}), {literal})"));
    }

    public string ToQueryString(string separator = "&")
    {
        var parts = new List<string>();

        if (_filters.Count > 0)
            parts.Add("$filter=" + Uri.EscapeDataString(string.Join(" and ", _filters.Select(f => $"({f})"))));

        if (_orderBy.Count > 0)
            parts.Add("$orderby=" + Uri.EscapeDataString(string.Join(",", _orderBy)));

        if (Skip is > 0)
            parts.Add($"$skip={Skip}");

        if (Top is > 0)
            parts.Add($"$top={Top}");

        if (Count)
            parts.Add("$count=true");

        return parts.Count == 0 ? string.Empty : separator + string.Join("&", parts);
    }
}
