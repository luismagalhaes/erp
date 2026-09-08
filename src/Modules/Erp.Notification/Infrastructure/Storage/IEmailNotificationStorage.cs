using Erp.Notification.Domain.Models;
using Erp.Notification.Infrastructure.Contracts;

namespace Erp.Notification.Infrastructure.Storage;

public interface IEmailNotificationStorage
{
    Task AddAsync(EmailNotification notification, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmailNotification>> GetPendingAsync(int take, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmailNotification>> GetHistoryAsync(CancellationToken cancellationToken = default);

    /// <summary>The history left unmaterialised so the grid OData options run in the database.</summary>
    IQueryable<EmailNotificationListItemDto> QueryHistory();
    Task<EmailNotification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
