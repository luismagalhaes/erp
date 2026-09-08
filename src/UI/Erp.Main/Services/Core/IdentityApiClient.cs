using System.Net.Http.Json;
using Erp.Common;
using Erp.Main.Models.Core;
using Erp.Main.Models.Inventory;
using Erp.Main.Models.Notification;
using Erp.Main.Models.Purchasing;
using Erp.Main.Models.Sales;
using Erp.Main.Models.SeriesRegistry;

namespace Erp.Main.Services;

public class IdentityApiClient(HttpClient http) : ApiClientBase(http)
{
    /// <summary>Users from the Identity store, so they can be assigned to companies.</summary>
    public async Task<(IReadOnlyList<IdentityUser> Users, string? Error)> GetUsersAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync("api/users", cancellationToken);

        if (!response.IsSuccessStatusCode)
            return ([], await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var users = await response.Content.ReadFromJsonAsync<List<IdentityUser>>(cancellationToken);
        return (users ?? [], null);
    }
}

