using Erp.Notification.Domain.Models;

namespace Erp.Notification.Infrastructure.Application;

public interface IEmailHistoryService
{
    Task<IReadOnlyList<EmailNotification>> GetHistoryAsync(CancellationToken cancellationToken = default);
    Task<EmailNotification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> RequeueFailedAsync(Guid id, CancellationToken cancellationToken = default);
}
