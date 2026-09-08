using System.Net.Http.Json;
using Erp.Common;
using Erp.Main.Models.Core;
using Erp.Main.Models.Inventory;
using Erp.Main.Models.Notification;
using Erp.Main.Models.Purchasing;
using Erp.Main.Models.Sales;
using Erp.Main.Models.SeriesRegistry;

namespace Erp.Main.Services;

public class NotificationApiClient(HttpClient http) : ApiClientBase(http)
{
    public async Task<IReadOnlyList<NotificationListItem>> GetNotificationsAsync(CancellationToken cancellationToken = default)
    {
        var notifications = await Http.GetFromJsonAsync<List<NotificationListItem>>("api/notifications", cancellationToken);
        return notifications ?? [];
    }

    /// <summary>
    /// Server side email listing. The queue is global, so the companyId the grid passes is ignored
    /// here.
    /// </summary>
    public Task<(IReadOnlyList<NotificationListItem> Items, int Count, string? Error)> QueryNotificationsAsync(
        ODataQuery query, CancellationToken cancellationToken = default) =>
        GetODataAsync<NotificationListItem>($"api/notifications/odata?{query.ToQueryString().TrimStart('&')}", cancellationToken);

    public async Task<NotificationDetail?> GetNotificationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync($"api/notifications/{id}", cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<NotificationDetail>(cancellationToken)
            : null;
    }

    /// <summary>Puts a failed email back in the queue. Returns the API message when it refuses.</summary>
    public async Task<string?> RequeueAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsync($"api/notifications/{id}/requeue", content: null, cancellationToken);
        return response.IsSuccessStatusCode ? null : await ApiResponse.ReadErrorAsync(response, cancellationToken);
    }
}

