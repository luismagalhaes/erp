using Erp.Core.Infrastructure.Contracts;

namespace Erp.Core.Infrastructure.Application;

/// <summary>
/// A company managing its own people, as opposed to <see cref="IUserCompanyAdminService"/>, which is
/// the SuperAdmin's view of every membership. Whoever belongs to the company may use it, so it never
/// reaches another company's rows.
/// </summary>
public interface ICompanyMemberService
{
    Task<CompanyMembersDto> GetAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds the person behind the email to the company: an existing account is associated straight
    /// away, otherwise an invitation is raised on the Identity host, which emails them.
    /// </summary>
    /// <exception cref="ArgumentException">The email is empty or malformed, or the company does not exist.</exception>
    /// <exception cref="InvalidOperationException">The account already belongs to the company.</exception>
    Task<AddCompanyMemberResult> AddAsync(
        Guid companyId, AddCompanyMemberRequest request, string? invitedBy, CancellationToken cancellationToken = default);

    /// <exception cref="InvalidOperationException">It is the last active member: a company nobody belongs to is unreachable.</exception>
    Task<bool> RemoveAsync(Guid companyId, Guid membershipId, CancellationToken cancellationToken = default);

    Task<bool> CancelInvitationAsync(Guid companyId, Guid invitationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Turns the invitations waiting for this account into memberships, and returns how many. Meant
    /// to run when someone opens the application, so a person who signed up from an invitation
    /// finds the company already there.
    /// </summary>
    Task<int> ClaimInvitationsAsync(string userId, CancellationToken cancellationToken = default);
}
