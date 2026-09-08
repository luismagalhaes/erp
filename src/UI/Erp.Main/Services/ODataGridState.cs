using System.Globalization;
using MudBlazor;

namespace Erp.Main.Services;

/// <summary>
/// Translates the state of a MudDataGrid into an OData query. Every server driven grid needs the
/// same translation, so it lives here instead of being copied into each page.
/// </summary>
public static class ODataGridState
{
    /// <summary>
    /// Builds the query for the window the grid is about to paint. Filtering, sorting and paging
    /// stay in the database and the browser only ever holds the rows around the scroll position.
    /// </summary>
    /// <param name="state">The window and the filters the grid is asking for.</param>
    /// <param name="search">The free text typed in the toolbar, if any.</param>
    /// <param name="searchFields">The members the free text is matched against.</param>
    /// <param name="defaultOrderBy">
    /// The member used to order when the user has not sorted. The order has to be stable, otherwise
    /// the same row could come back in two different windows as the user scrolls.
    /// </param>
    public static ODataQuery Build<T>(
        GridStateVirtualize<T> state,
        string? search,
        IReadOnlyList<string> searchFields,
        string defaultOrderBy)
    {
        var query = new ODataQuery
        {
            Skip = state.StartIndex,
            Top = state.Count
        };

        if (!string.IsNullOrWhiteSpace(search) && searchFields.Count > 0)
            query.Filter(ODataQuery.ContainsAny(search.Trim(), [.. searchFields]));

        foreach (var filter in state.FilterDefinitions)
            query.Filter(BuildFilter(filter));

        foreach (var sort in state.SortDefinitions)
            query.OrderBy(sort.SortBy, sort.Descending);

        if (state.SortDefinitions.Count == 0)
            query.OrderBy(defaultOrderBy, descending: false);

        return query;
    }

    /// <summary>
    /// Converts a single column filter into its OData expression. Unsupported combinations return
    /// null so they are simply skipped rather than producing a request the server rejects.
    /// </summary>
    public static string? BuildFilter<T>(IFilterDefinition<T> filter)
    {
        var field = filter.Column?.PropertyName;

        if (string.IsNullOrWhiteSpace(field) || string.IsNullOrWhiteSpace(filter.Operator))
            return null;

        return filter.Operator switch
        {
            FilterOperator.String.Empty => $"{field} eq null or {field} eq ''",
            FilterOperator.String.NotEmpty => $"{field} ne null and {field} ne ''",
            _ when filter.Value is null => null,
            // A multi value selection becomes an OR of equalities, which is what OData understands.
            _ when filter.Value is IReadOnlyCollection<string> values =>
                values.Count == 0
                    ? null
                    : $"({string.Join(" or ", values.Select(item => $"{field} eq {ODataQuery.Literal(item)}"))})",
            FilterOperator.String.Contains => $"contains(tolower({field}), {Lowered(filter.Value)})",
            FilterOperator.String.NotContains => $"not contains(tolower({field}), {Lowered(filter.Value)})",
            FilterOperator.String.StartsWith => $"startswith(tolower({field}), {Lowered(filter.Value)})",
            FilterOperator.String.EndsWith => $"endswith(tolower({field}), {Lowered(filter.Value)})",
            FilterOperator.String.Equal or FilterOperator.Number.Equal => $"{field} eq {Value(filter.Value)}",
            FilterOperator.String.NotEqual or FilterOperator.Number.NotEqual => $"{field} ne {Value(filter.Value)}",
            FilterOperator.Number.GreaterThan => $"{field} gt {Value(filter.Value)}",
            FilterOperator.Number.GreaterThanOrEqual => $"{field} ge {Value(filter.Value)}",
            FilterOperator.Number.LessThan => $"{field} lt {Value(filter.Value)}",
            FilterOperator.Number.LessThanOrEqual => $"{field} le {Value(filter.Value)}",
            FilterOperator.Boolean.Is => $"{field} eq {Value(filter.Value)}",
            _ => null
        };
    }

    private static string Lowered(object value) =>
        ODataQuery.Literal(value.ToString()!.ToLowerInvariant());

    /// <summary>Renders a value as an OData literal of the right type.</summary>
    public static string Value(object value) => value switch
    {
        string text => ODataQuery.Literal(text),
        bool flag => flag ? "true" : "false",
        Guid id => id.ToString(),
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        _ => ODataQuery.Literal(value.ToString() ?? string.Empty)
    };
}
