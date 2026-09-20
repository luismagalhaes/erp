using Erp.Identity.Domain.Application;

namespace Erp.Identity.Infrastructure.Application;

public interface ILoginAuditService
{
    Task RecordAsync(
        string? userId,
        string email,
        bool succeeded,
        string? failureReason,
        string? remoteIp,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LoginAuditListItem>> GetRecentAsync(CancellationToken cancellationToken = default);
}
