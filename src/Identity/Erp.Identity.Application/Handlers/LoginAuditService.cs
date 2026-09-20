using Erp.Identity.Domain.Application;
using Erp.Identity.Infrastructure.Application;
using Erp.Identity.Infrastructure.Storage;

namespace Erp.Identity.Application.Handlers;

public sealed class LoginAuditService(ILoginAuditStorage storage) : ILoginAuditService
{
    /// <summary>The backoffice page shows recent activity, not the entire history ever recorded.</summary>
    private const int RecentTake = 500;

    public Task RecordAsync(
        string? userId,
        string email,
        bool succeeded,
        string? failureReason,
        string? remoteIp,
        CancellationToken cancellationToken = default)
        => storage.RecordAsync(userId, email, succeeded, failureReason, remoteIp, cancellationToken);

    public Task<IReadOnlyList<LoginAuditListItem>> GetRecentAsync(CancellationToken cancellationToken = default)
        => storage.GetRecentAsync(RecentTake, cancellationToken);
}
