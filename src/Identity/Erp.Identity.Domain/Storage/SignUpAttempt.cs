namespace Erp.Identity.Data;

/// <summary>
/// One row per submission of <c>/Account/SignUp</c>, successful or not — sign-up has no "wrong
/// password" the way a login does, so this counts plain submission volume instead of failures.
/// Persisted (not in-memory) so the reCAPTCHA threshold it feeds stays correct across every
/// instance of the host, the same way <see cref="LoginAudit"/> already does for sign-in.
/// </summary>
public sealed class SignUpAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string? RemoteIp { get; set; }

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
