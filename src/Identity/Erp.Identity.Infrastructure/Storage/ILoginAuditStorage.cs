using Erp.Identity.Domain.Application;

namespace Erp.Identity.Infrastructure.Storage;

public interface ILoginAuditStorage
{
    Task RecordAsync(
        string? userId,
        string email,
        bool succeeded,
        string? failureReason,
        string? remoteIp,
        CancellationToken cancellationToken = default);

    /// <param name="take">Capped read — a session log is for the most recent activity, not an
    /// unbounded table the backoffice would otherwise have to load in full.</param>
    Task<IReadOnlyList<LoginAuditListItem>> GetRecentAsync(int take, CancellationToken cancellationToken = default);
}
