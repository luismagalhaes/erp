using System.Net.Http.Json;
using Erp.Main.Models.SeriesRegistry;

namespace Erp.Main.Services;

public class SeriesApiClient(HttpClient http) : ApiClientBase(http)
{
    public async Task<IReadOnlyList<SalesSeries>> GetSeriesAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var series = await Http.GetFromJsonAsync<List<SalesSeries>>(
            $"api/series?companyId={companyId}", cancellationToken);

        return series ?? [];
    }

    /// <summary>Server side series listing, filtered, sorted and paged by the database.</summary>
    public Task<(IReadOnlyList<SalesSeries> Items, int Count, string? Error)> QuerySeriesAsync(
        Guid companyId, ODataQuery query, CancellationToken cancellationToken = default) =>
        GetODataAsync<SalesSeries>($"api/series/odata?companyId={companyId}{query.ToQueryString()}", cancellationToken);

    public async Task<(SalesSeries? Series, string? Error)> CreateSeriesAsync(
        CreateSeriesRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync("api/series", request, cancellationToken);

        return response.IsSuccessStatusCode
            ? (await response.Content.ReadFromJsonAsync<SalesSeries>(cancellationToken), null)
            : (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));
    }

    /// <summary>
    /// Changes what documents of the series do to stock — the only thing a series still allows to
    /// be changed once it exists.
    /// </summary>
    public async Task<(SalesSeries? Series, string? Error)> UpdateSeriesAsync(
        Guid id,
        string stockEffect,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.PutAsJsonAsync(
            $"api/series/{id}", new UpdateSeriesRequest(stockEffect), cancellationToken);

        return response.IsSuccessStatusCode
            ? (await response.Content.ReadFromJsonAsync<SalesSeries>(cancellationToken), null)
            : (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));
    }

    public async Task<string?> CommunicateSeriesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsync($"api/series/{id}/communicate", null, cancellationToken);

        return response.IsSuccessStatusCode ? null : await ApiResponse.ReadErrorAsync(response, cancellationToken);
    }

    /// <summary>Records a validation code obtained outside the webservice, e.g. straight from the Portal das Finanças.</summary>
    public async Task<string?> CommunicateSeriesManuallyAsync(
        Guid id, string validationCode, CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync(
            $"api/series/{id}/communicate-manually", new CommunicateSeriesManuallyRequest(validationCode), cancellationToken);

        return response.IsSuccessStatusCode ? null : await ApiResponse.ReadErrorAsync(response, cancellationToken);
    }

    /// <summary>Cancels a series communicated by mistake, before any document was issued on it.</summary>
    public async Task<string?> CancelSeriesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsync($"api/series/{id}/cancel", null, cancellationToken);

        return response.IsSuccessStatusCode ? null : await ApiResponse.ReadErrorAsync(response, cancellationToken);
    }

    public async Task<string?> FinalizeSeriesAsync(
        Guid id, string? justificacao, CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync(
            $"api/series/{id}/finalize", new FinalizeSeriesRequest(justificacao), cancellationToken);

        return response.IsSuccessStatusCode ? null : await ApiResponse.ReadErrorAsync(response, cancellationToken);
    }
}
