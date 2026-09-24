namespace Erp.Dependencies.IdentityOnboarding;

/// <summary>Mirrors the Identity host's onboarding request, the invitation as it stores it.</summary>
public sealed record IdentityOnboardingRequestDto(
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
