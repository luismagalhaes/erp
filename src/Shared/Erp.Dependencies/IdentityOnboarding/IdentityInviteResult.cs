namespace Erp.Dependencies.IdentityOnboarding;

/// <summary>
/// The answer to an invitation: the id of the account that already uses the email (nothing was
/// raised then), or the request that was raised.
/// </summary>
public sealed record IdentityInviteResult(string? ExistingUserId, IdentityOnboardingRequestDto? Request);
