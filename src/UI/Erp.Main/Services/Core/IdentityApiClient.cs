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

    /// <summary>
    /// Finishes sign-up by granting the signed-in user the User role, once their company exists.
    /// Signing up leaves an account with no role at all, so this is what makes it a usable one.
    /// </summary>
    public async Task<string?> CompleteOnboardingAsync(CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsync("api/self-service/complete-onboarding", null, cancellationToken);
        return response.IsSuccessStatusCode ? null : await ApiResponse.ReadErrorAsync(response, cancellationToken);
    }
}

