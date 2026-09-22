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

    /// <summary>How many failed sign-ins this IP has racked up within <paramref name="window"/> —
    /// what decides whether reCAPTCHA is required yet.</summary>
    Task<int> CountRecentFailuresAsync(string remoteIp, TimeSpan window, CancellationToken cancellationToken = default);
}
