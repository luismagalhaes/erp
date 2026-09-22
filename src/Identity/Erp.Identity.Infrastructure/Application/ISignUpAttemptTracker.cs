namespace Erp.Identity.Infrastructure.Application;

/// <summary>
/// Counts sign-up form submissions per IP so reCAPTCHA only kicks in once a few have gone
/// through — sign-up has no "failed attempt" concept the way a login does, so this tracks plain
/// submission volume instead of reusing <see cref="ILoginAuditService"/>.
/// </summary>
public interface ISignUpAttemptTracker
{
    /// <summary>How many submissions this IP has made within <paramref name="window"/>, without
    /// recording a new one — what a page checks on render to decide whether to require a token.</summary>
    Task<int> GetCountAsync(string? remoteIp, TimeSpan window, CancellationToken cancellationToken = default);

    /// <summary>Call once per actual form submission, regardless of outcome.</summary>
    Task RecordAttemptAsync(string? remoteIp, CancellationToken cancellationToken = default);
}
