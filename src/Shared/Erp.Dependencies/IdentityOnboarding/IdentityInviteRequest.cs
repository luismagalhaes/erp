namespace Erp.Dependencies.IdentityOnboarding;

/// <summary>Mirrors the Identity host's request to invite an email address to a company.</summary>
public sealed record IdentityInviteRequest(
    string Email,
    Guid CompanyId,
    string CompanyName,
    string Role,
    string? InvitedByEmail = null,
    string? Culture = null);
