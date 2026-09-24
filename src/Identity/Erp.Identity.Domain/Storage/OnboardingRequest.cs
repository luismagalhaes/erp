namespace Erp.Identity.Data;

/// <summary>
/// An invitation to join a company, for a person who has no account here yet. The ERP API is the
/// authority on who may invite whom and raises these; this host stores them, emails the invitation
/// and shows them in the backoffice. Once the invited person has an account the ERP API hands the
/// membership over and marks the request completed.
/// </summary>
public sealed class OnboardingRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Where the invitation was sent, stored lower case so it matches the account's email.</summary>
    public string Email { get; set; } = string.Empty;

    public Guid CompanyId { get; set; }

    public string CompanyName { get; set; } = string.Empty;

    /// <summary>The role the person gets in the company once they join.</summary>
    public string Role { get; set; } = string.Empty;

    public string? InvitedByEmail { get; set; }

    /// <summary>"Pending", "Completed" or "Cancelled".</summary>
    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>When the invitation email was last sent, so a resend is visible in the backoffice.</summary>
    public DateTime LastSentAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAtUtc { get; set; }

    public string? CompletedUserId { get; set; }
}
