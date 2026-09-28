using System.Net.Http.Json;
using Erp.Main.Models.Core;

namespace Erp.Main.Services;

/// <summary>
/// The test data actions of a company: issuing its demo sales and purchases, and wiping everything
/// it did. Kept apart from <see cref="CoreApiClient"/> because both touch hundreds of rows, which
/// takes far longer than the timeout the other clients are held to.
/// </summary>
public sealed class DemoHistoryApiClient(HttpClient http) : ApiClientBase(http)
{
    public async Task<(DemoHistoryResult? Result, string? Error)> ApplyAsync(
        Guid companyId, CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsync($"api/companies/{companyId}/apply-demo-history", null, cancellationToken);

        return response.IsSuccessStatusCode
            ? (await response.Content.ReadFromJsonAsync<DemoHistoryResult>(cancellationToken), null)
            : (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));
    }

    /// <summary>
    /// Deletes everything the company did and set up, keeping the company itself. Returns how many
    /// rows were deleted in all. The company's tax id is sent back as the API's guard against
    /// wiping the wrong one.
    /// </summary>
    public async Task<(int Rows, string? Error)> PurgeAsync(
        Guid companyId, string taxId, CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsync(
            $"api/companies/{companyId}/purge-data?confirmTaxId={Uri.EscapeDataString(taxId)}", null, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (0, await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var deleted = await response.Content.ReadFromJsonAsync<Dictionary<string, int>>(cancellationToken);

        return (deleted?.Values.Sum() ?? 0, null);
    }
}
