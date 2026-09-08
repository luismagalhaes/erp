using Erp.Notification.Domain.Models;
using Erp.Notification.Infrastructure.Storage;
using Erp.Notification.Storage.Data;
using Microsoft.EntityFrameworkCore;
using Erp.Storage;

namespace Erp.Notification.Storage.Storage;

public sealed class EmailNotificationStorage(ErpDbContext dbContext) : IEmailNotificationStorage
{
    public async Task AddAsync(EmailNotification notification, CancellationToken cancellationToken = default)
    {
        await dbContext.Set<EmailNotification>().AddAsync(notification, cancellationToken);
    }

    public async Task<IReadOnlyList<EmailNotification>> GetPendingAsync(int take, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<EmailNotification>()
            .Where(x => x.Status == EmailNotificationStatus.Pending || x.Status == EmailNotificationStatus.Failed)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmailNotification>> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<EmailNotification>()
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<EmailNotification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Set<EmailNotification>()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
