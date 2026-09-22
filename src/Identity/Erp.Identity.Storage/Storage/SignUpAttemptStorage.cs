using Erp.Identity.Data;
using Erp.Identity.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace Erp.Identity.Storage.Storage;

public sealed class SignUpAttemptStorage(IDbContextFactory<ApplicationDbContext> dbContextFactory) : ISignUpAttemptStorage
{
    public async Task RecordAsync(string? remoteIp, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        dbContext.SignUpAttempts.Add(new SignUpAttempt { RemoteIp = remoteIp });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CountRecentAsync(string? remoteIp, TimeSpan window, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var since = DateTime.UtcNow - window;
        return await dbContext.SignUpAttempts.CountAsync(
            x => x.RemoteIp == remoteIp && x.OccurredAtUtc >= since, cancellationToken);
    }
}
