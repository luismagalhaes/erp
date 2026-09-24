namespace Erp.Dependencies.IdentityOnboarding;

/// <summary>
/// The Identity host's onboarding requests, as this API uses them. The users live there, so
/// whether an email already has an account, and the invitations for those that do not, are asked of
/// it rather than kept in a second copy here.
/// </summary>
public interface IIdentityOnboardingClient
{
    Task<IdentityInviteResult> InviteAsync(IdentityInviteRequest request, CancellationToken cancellationToken = default);

    /// <summary>Every request raised for the company, whatever its status.</summary>
    Task<IReadOnlyList<IdentityOnboardingRequestDto>> GetForCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>The pending requests waiting for this account. Empty until its email is confirmed.</summary>
    Task<IReadOnlyList<IdentityOnboardingRequestDto>> GetPendingForUserAsync(string userId, CancellationToken cancellationToken = default);

    Task<bool> CompleteAsync(Guid requestId, string userId, CancellationToken cancellationToken = default);

    Task<bool> CancelAsync(Guid requestId, CancellationToken cancellationToken = default);
}
