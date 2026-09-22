namespace Erp.Identity.Infrastructure.Storage;

public interface ISignUpAttemptStorage
{
    Task RecordAsync(string? remoteIp, CancellationToken cancellationToken = default);

    /// <summary>How many sign-up submissions this IP has made within <paramref name="window"/> —
    /// what decides whether reCAPTCHA is required yet.</summary>
    Task<int> CountRecentAsync(string? remoteIp, TimeSpan window, CancellationToken cancellationToken = default);
}
