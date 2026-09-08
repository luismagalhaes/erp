using Erp.Notification.Domain.Models;
using Erp.Notification.Infrastructure.Contracts;

namespace Erp.Notification.Infrastructure.Application;

public interface IEmailHistoryService
{
    Task<IReadOnlyList<EmailNotification>> GetHistoryAsync(CancellationToken cancellationToken = default);

    /// <summary>The history as a queryable, so the grid endpoint can push its OData options down.</summary>
    IQueryable<EmailNotificationListItemDto> QueryHistory();
    Task<EmailNotification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> RequeueFailedAsync(Guid id, CancellationToken cancellationToken = default);
}
