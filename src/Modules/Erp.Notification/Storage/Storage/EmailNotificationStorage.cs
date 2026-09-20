using Erp.Notification.Domain.Models;
using Erp.Notification.Infrastructure.Contracts;
using Erp.Notification.Infrastructure.Storage;
using Erp.Notification.Storage.Data;
using Microsoft.EntityFrameworkCore;
using Erp.Storage;

namespace Erp.Notification.Storage.Storage;

public sealed class EmailNotificationStorage(AppDbContext dbContext) : IEmailNotificationStorage
{
    public async Task AddAsync(EmailNotification notification, CancellationToken cancellationToken = default)
    {
        await dbContext.Set<EmailNotification>().AddAsync(notification, cancellationToken);
    }

    public async Task<IReadOnlyList<EmailNotification>> GetPendingAsync(int take, int maxRetries, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<EmailNotification>()
            .Where(x => x.Status == EmailNotificationStatus.Pending
                || (x.Status == EmailNotificationStatus.Failed && x.RetryCount < maxRetries))
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

    public IQueryable<EmailNotificationListItemDto> QueryHistory() =>
        dbContext.Set<EmailNotification>()
            .AsNoTracking()
            // Member initialization, not a constructor call: EF Core only keeps the mapping between
            // the DTO members and the columns this way, which is what lets the OData $filter and
            // $orderby applied afterwards be translated to SQL. The status is spelled out as a
            // conditional for the same reason: ToString on the enum has no SQL translation.
            .Select(x => new EmailNotificationListItemDto
            {
                Id = x.Id,
                ToEmail = x.ToEmail,
                Subject = x.Subject,
                Status = x.Status == EmailNotificationStatus.Sent
                    ? "Sent"
                    : x.Status == EmailNotificationStatus.Failed
                        ? "Failed"
                        : "Pending",
                RetryCount = x.RetryCount,
                LastError = x.LastError,
                CreatedAtUtc = x.CreatedAtUtc,
                ProcessedAtUtc = x.ProcessedAtUtc
            });

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
