namespace Erp.Identity.Data;

/// <summary>
/// One row per sign-in attempt against <c>/authentication/login</c>, successful or not. Never the
/// password — the point is to see who signed in and when, not to keep a second copy of credentials.
/// </summary>
public sealed class LoginAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Null when the attempt failed before a matching account was found.</summary>
    public string? UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public bool Succeeded { get; set; }

    /// <summary>"InvalidCredentials", "LockedOut" or "EmailNotConfirmed" — null when it succeeded.</summary>
    public string? FailureReason { get; set; }

    public string? RemoteIp { get; set; }

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
