using Erp.Common;
using Microsoft.AspNetCore.OData.Query;

namespace Erp.Api.Services;

/// <summary>
/// Applies OData query options to a queryable and wraps the result in the collection envelope the
/// client grids expect. Every list route needs the same sequence, so it is written once here.
/// </summary>
public static class ODataQueryExecutor
{
    /// <summary>
    /// Runs the query options against <paramref name="source"/>.
    /// The options are applied by hand instead of through [EnableQuery] because these are plain MVC
    /// routes: without an EDM route the attribute cannot emit the { value, @odata.count } envelope
    /// the grids expect, and $count would fail. The count is taken after $filter but before
    /// $skip/$top so the pager knows the size of the whole result rather than of the current page.
    /// </summary>
    public static ODataCollection<T> Execute<T>(IQueryable<T> source, ODataQueryOptions<T> options)
    {
        var settings = new ODataQuerySettings();
        var query = source;

        if (options.Filter is not null)
            query = (IQueryable<T>)options.Filter.ApplyTo(query, settings);

        var count = query.Count();

        if (options.OrderBy is not null)
            query = options.OrderBy.ApplyTo(query, settings);

        if (options.Skip is not null)
            query = options.Skip.ApplyTo(query, settings);

        query = options.Top is not null
            ? options.Top.ApplyTo(query, settings)
            : query.Take(Constants.ODataQueryLimits.MaxPageSize);

        return new ODataCollection<T>(count, [.. query]);
    }
}
