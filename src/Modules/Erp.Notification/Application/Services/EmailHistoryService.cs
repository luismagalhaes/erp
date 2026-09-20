using Erp.Notification.Domain.Models;
using Erp.Notification.Infrastructure.Application;
using Erp.Notification.Infrastructure.Contracts;
using Erp.Notification.Infrastructure.Storage;

namespace Erp.Notification.Application.Services;

public sealed class EmailHistoryService(IEmailNotificationStorage storage) : IEmailHistoryService
{
    public Task<IReadOnlyList<EmailNotification>> GetHistoryAsync(CancellationToken cancellationToken = default)
        => storage.GetHistoryAsync(cancellationToken);

    public IQueryable<EmailNotificationListItemDto> QueryHistory() => storage.QueryHistory();

    public Task<EmailNotification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => storage.GetByIdAsync(id, cancellationToken);

    public async Task<bool> RequeueFailedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var notification = await storage.GetByIdAsync(id, cancellationToken);
        if (notification is null || notification.Status != EmailNotificationStatus.Failed)
            return false;

        // A fresh RetryCount too: a notification that had already used up its automatic attempts
        // gets a full new run at it, not just the one attempt left over from before someone stepped
        // in by hand.
        notification.Status = EmailNotificationStatus.Pending;
        notification.RetryCount = 0;
        notification.LastError = null;
        notification.ProcessedAtUtc = null;

        await storage.SaveChangesAsync(cancellationToken);
        return true;
    }
}
