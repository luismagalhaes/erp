using System.Net.Http.Json;

namespace Erp.Main.Services;

/// <summary>
/// What every API client needs to talk to the ERP API: the reads the screens make, and the single
/// place where a failed response is turned into a message. The clients differ by route, not by
/// plumbing, so the plumbing is written once.
/// </summary>
public abstract class ApiClientBase(HttpClient http)
{
    public HttpClient Http { get; } = http;

    /// <summary>
    /// Reads one window of a server driven listing. The API answers with the OData envelope, so the
    /// count returned is the size of the whole filtered result and not of the window, which is what
    /// lets a virtualized grid know how far it can scroll.
    /// </summary>
    protected async Task<(IReadOnlyList<T> Items, int Count, string? Error)> GetODataAsync<T>(
        string route,
        CancellationToken cancellationToken)
    {
        var response = await Http.GetAsync(route, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return ([], 0, await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var payload = await response.Content.ReadFromJsonAsync<ODataResponse<T>>(cancellationToken);
        return (payload?.Value ?? [], payload?.Count ?? 0, null);
    }

    protected async Task<(IReadOnlyList<T> Items, string? Error)> GetListAsync<T>(
        string route,
        CancellationToken cancellationToken)
    {
        var response = await Http.GetAsync(route, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return ([], await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var items = await response.Content.ReadFromJsonAsync<List<T>>(cancellationToken);
        return (items ?? [], null);
    }

    protected async Task<T?> GetSingleAsync<T>(string route, CancellationToken cancellationToken)
    {
        var response = await Http.GetAsync(route, cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<T>(cancellationToken)
            : default;
    }

    /// <summary>Like <see cref="GetSingleAsync{T}"/>, but says why when the API refuses.</summary>
    protected async Task<(T? Item, string? Error)> GetSingleOrErrorAsync<T>(string route, CancellationToken cancellationToken)
    {
        var response = await Http.GetAsync(route, cancellationToken);

        return response.IsSuccessStatusCode
            ? (await response.Content.ReadFromJsonAsync<T>(cancellationToken), null)
            : (default, await ApiResponse.ReadErrorAsync(response, cancellationToken));
    }

    /// <summary>The period of a statement, as the query string the statement endpoints read.</summary>
    protected static string PeriodQuery(DateOnly? startDate, DateOnly? endDate) =>
        (startDate is { } start ? $"&startDate={start:yyyy-MM-dd}" : string.Empty)
        + (endDate is { } end ? $"&endDate={end:yyyy-MM-dd}" : string.Empty);

    /// <summary>Returns null when it worked, or the message the API gave for refusing.</summary>
    protected static async Task<string?> SendAsync(
        Func<Task<HttpResponseMessage>> send,
        CancellationToken cancellationToken)
    {
        var response = await send();
        return response.IsSuccessStatusCode
            ? null
            : await ApiResponse.ReadErrorAsync(response, cancellationToken);
    }
}
