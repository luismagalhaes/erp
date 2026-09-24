using System.ComponentModel.DataAnnotations;
using Erp.Common;
using Erp.Core.Domain;
using Erp.Core.Infrastructure.Application;
using Erp.Dependencies.IdentityOnboarding;
using Erp.Core.Infrastructure.Contracts;
using Erp.Core.Infrastructure.Storage;
using Microsoft.Extensions.Logging;

namespace Erp.Core.Application.Services;

public sealed class CompanyMemberService(
    IUserCompanyStorage userCompanyStorage,
    ICompanyStorage companyStorage,
    IIdentityOnboardingClient identityOnboardingClient,
    IUnitOfWork unitOfWork,
    ILogger<CompanyMemberService> logger) : ICompanyMemberService
{
    /// <summary>Same value Identity stores, so a request is only ever read back as pending, never assumed.</summary>
    private const string PendingStatus = "Pending";

    public async Task<CompanyMembersDto> GetAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var memberships = await userCompanyStorage.GetAllAsync(companyId, cancellationToken);
        var members = memberships.Select(Map).ToList();

        try
        {
            var requests = await identityOnboardingClient.GetForCompanyAsync(companyId, cancellationToken);

            return new CompanyMembersDto(
                members,
                [.. requests.Where(x => x.Status == PendingStatus).Select(MapInvitation)],
                InvitationsAvailable: true);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            // The members are this API's own data and must still show when the Identity host is
            // down or not configured; only the invitations, which live there, are missing.
            logger.LogWarning(ex, "Could not read the pending invitations of company {CompanyId} from the Identity host.", companyId);
            return new CompanyMembersDto(members, [], InvitationsAvailable: false);
        }
    }

    public async Task<AddCompanyMemberResult> AddAsync(
        Guid companyId, AddCompanyMemberRequest request, string? invitedBy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = (request.Email ?? string.Empty).Trim();

        if (email.Length == 0 || !new EmailAddressAttribute().IsValid(email))
            throw new ArgumentException("A valid email is required.", nameof(request));

        var company = await companyStorage.GetByIdAsync(companyId, cancellationToken)
            ?? throw new ArgumentException($"Company '{companyId}' was not found.", nameof(companyId));

        var result = await identityOnboardingClient.InviteAsync(
            new IdentityInviteRequest(email, company.Id, company.Name, Constants.Roles.User, invitedBy, request.Culture),
            cancellationToken);

        if (result.Request is { } invitation)
            return new AddCompanyMemberResult(AddCompanyMemberOutcomes.Invited, null, MapInvitation(invitation));

        var userId = result.ExistingUserId
            ?? throw new InvalidOperationException("The Identity host answered with neither an account nor an invitation.");

        var member = await AssociateAsync(company.Id, userId, Constants.Roles.User, cancellationToken);
        return new AddCompanyMemberResult(AddCompanyMemberOutcomes.Added, member, null);
    }

    public async Task<bool> RemoveAsync(Guid companyId, Guid membershipId, CancellationToken cancellationToken = default)
    {
        var memberships = await userCompanyStorage.GetAllAsync(companyId, cancellationToken);
        var target = memberships.FirstOrDefault(x => x.Id == membershipId);

        if (target is null)
            return false;

        if (target.IsActive && memberships.Count(x => x.IsActive) <= 1)
        {
            throw new InvalidOperationException(
                "A company must keep at least one member. Add someone else before removing this one.");
        }

        // GetAllAsync reads untracked; the tracked copy is the one that can be removed.
        var tracked = await userCompanyStorage.GetByIdAsync(membershipId, cancellationToken);

        if (tracked is null)
            return false;

        userCompanyStorage.Remove(tracked);
        await userCompanyStorage.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CancelInvitationAsync(Guid companyId, Guid invitationId, CancellationToken cancellationToken = default)
    {
        // Checked against this company's own requests, so an id from another company is simply not found.
        var requests = await identityOnboardingClient.GetForCompanyAsync(companyId, cancellationToken);

        return requests.Any(x => x.Id == invitationId && x.Status == PendingStatus)
            && await identityOnboardingClient.CancelAsync(invitationId, cancellationToken);
    }

    public async Task<int> ClaimInvitationsAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var pending = await identityOnboardingClient.GetPendingForUserAsync(userId, cancellationToken);
        var claimed = 0;

        foreach (var invitation in pending)
        {
            try
            {
                // The person is not a member yet, which is the whole point of the invitation, so the
                // tenant guard would refuse a row that is theirs to receive. See RunUnrestrictedAsync.
                await unitOfWork.RunUnrestrictedAsync(async () =>
                {
                    if (await userCompanyStorage.CompanyExistsAsync(invitation.CompanyId, cancellationToken))
                    {
                        await AssociateAsync(invitation.CompanyId, userId, invitation.Role, cancellationToken, tolerateExisting: true);
                        claimed++;
                    }
                });

                await identityOnboardingClient.CompleteAsync(invitation.Id, userId, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
            {
                // One bad invitation must not stop the others, nor the caller opening the application.
                logger.LogWarning(ex, "Could not take up invitation {InvitationId} for user {UserId}.", invitation.Id, userId);
            }
        }

        return claimed;
    }

    /// <summary>
    /// Makes the account a member: adds the row, or reactivates one an earlier removal left inactive.
    /// </summary>
    private async Task<UserCompanyAdminDto> AssociateAsync(
        Guid companyId, string userId, string role, CancellationToken cancellationToken, bool tolerateExisting = false)
    {
        var existing = (await userCompanyStorage.GetAllAsync(companyId, cancellationToken))
            .FirstOrDefault(x => x.UserId == userId);

        if (existing is { IsActive: true })
        {
            if (!tolerateExisting)
                throw new InvalidOperationException("This user already belongs to the company.");

            return Map(existing);
        }

        if (existing is not null)
        {
            var tracked = await userCompanyStorage.GetByIdAsync(existing.Id, cancellationToken)
                ?? throw new InvalidOperationException("Unable to load the membership to reactivate.");

            tracked.IsActive = true;
            await userCompanyStorage.SaveChangesAsync(cancellationToken);
            return Map(tracked);
        }

        var entity = new UserCompany
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CompanyId = companyId,
            Role = role,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        await userCompanyStorage.AddAsync(entity, cancellationToken);
        await userCompanyStorage.SaveChangesAsync(cancellationToken);

        var persisted = await userCompanyStorage.GetByIdAsync(entity.Id, cancellationToken)
            ?? throw new InvalidOperationException("Unable to load the created membership.");

        return Map(persisted);
    }

    private static UserCompanyAdminDto Map(UserCompany entity) =>
        new(entity.Id, entity.UserId, entity.CompanyId, entity.Company.Name, entity.Role, entity.IsActive);

    private static CompanyInvitationDto MapInvitation(IdentityOnboardingRequestDto request) =>
        new(request.Id, request.Email, request.Role, request.InvitedByEmail, request.CreatedAtUtc, request.LastSentAtUtc);
}
