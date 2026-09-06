using Erp.Notification.Domain.Models;
using Erp.Notification.Infrastructure.Storage;
using Erp.Notification.Storage.Data;
using Microsoft.EntityFrameworkCore;

namespace Erp.Notification.Storage.Storage;

public sealed class EmailNotificationStorage(NotificationDbContext dbContext) : IEmailNotificationStorage
{
    public async Task AddAsync(EmailNotification notification, CancellationToken cancellationToken = default)
    {
        await dbContext.EmailNotifications.AddAsync(notification, cancellationToken);
    }

    public async Task<IReadOnlyList<EmailNotification>> GetPendingAsync(int take, CancellationToken cancellationToken = default)
    {
        return await dbContext.EmailNotifications
            .Where(x => x.Status == EmailNotificationStatus.Pending || x.Status == EmailNotificationStatus.Failed)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmailNotification>> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.EmailNotifications
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<EmailNotification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.EmailNotifications
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
