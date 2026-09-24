namespace Erp.Identity.Domain.Application;

/// <summary>
/// What inviting an email address led to: the account already exists, so there is nothing to
/// invite and the caller only has to associate it (<see cref="ExistingUserId"/>), or a request was
/// raised (<see cref="Request"/>) and the invitation email has to go out.
/// </summary>
public sealed record InviteToCompanyResult(string? ExistingUserId, OnboardingRequestListItem? Request)
{
    public bool UserExists => ExistingUserId is not null;
}
