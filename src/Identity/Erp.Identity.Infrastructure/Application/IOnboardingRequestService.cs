using Erp.Identity.Domain.Application;

namespace Erp.Identity.Infrastructure.Application;

public interface IOnboardingRequestService
{
    /// <summary>Every request, newest first, optionally limited to one company.</summary>
    Task<IReadOnlyList<OnboardingRequestListItem>> GetAllAsync(Guid? companyId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Raises a request for the email, unless an account already uses it. A pending request for the
    /// same email and company is refreshed instead of duplicated, which is how an invitation is resent.
    /// </summary>
    Task<InviteToCompanyResult> InviteAsync(InviteToCompanyRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// The pending requests waiting for this account, matched on its email and only once that email
    /// is confirmed, so nobody can claim an invitation by signing up with an address that is not theirs.
    /// </summary>
    Task<IReadOnlyList<OnboardingRequestListItem>> GetPendingForUserAsync(string userId, CancellationToken cancellationToken = default);

    Task<bool> CompleteAsync(Guid id, string userId, CancellationToken cancellationToken = default);

    Task<bool> CancelAsync(Guid id, CancellationToken cancellationToken = default);
}
