namespace Erp.Identity.Domain.Application;

public sealed record OnboardingRequestListItem(
    Guid Id,
    string Email,
    Guid CompanyId,
    string CompanyName,
    string Role,
    string? InvitedByEmail,
    string Status,
    DateTime CreatedAtUtc,
    DateTime LastSentAtUtc,
    DateTime? CompletedAtUtc);
