using Erp.Identity.Data;
using Erp.Identity.Domain.Application;
using Erp.Identity.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace Erp.Identity.Storage.Storage;

public sealed class LoginAuditStorage(IDbContextFactory<ApplicationDbContext> dbContextFactory) : ILoginAuditStorage
{
    public async Task RecordAsync(
        string? userId,
        string email,
        bool succeeded,
        string? failureReason,
        string? remoteIp,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        dbContext.LoginAudits.Add(new LoginAudit
        {
            UserId = userId,
            Email = email,
            Succeeded = succeeded,
            FailureReason = failureReason,
            RemoteIp = remoteIp
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LoginAuditListItem>> GetRecentAsync(int take, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.LoginAudits
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(take)
            .Select(x => new LoginAuditListItem(x.Id, x.UserId, x.Email, x.Succeeded, x.FailureReason, x.RemoteIp, x.OccurredAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountRecentFailuresAsync(string remoteIp, TimeSpan window, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var since = DateTime.UtcNow - window;
        return await dbContext.LoginAudits.CountAsync(
            x => x.RemoteIp == remoteIp && !x.Succeeded && x.OccurredAtUtc >= since, cancellationToken);
    }
}
