using Erp.Notification.Domain.Models;

namespace Erp.Notification.Infrastructure.Storage;

public interface IEmailNotificationStorage
{
    Task AddAsync(EmailNotification notification, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmailNotification>> GetPendingAsync(int take, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmailNotification>> GetHistoryAsync(CancellationToken cancellationToken = default);
    Task<EmailNotification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
