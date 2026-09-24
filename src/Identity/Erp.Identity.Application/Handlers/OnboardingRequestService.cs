using Erp.Identity.Common.Constants;
using Erp.Identity.Data;
using Erp.Identity.Domain.Application;
using Erp.Identity.Infrastructure.Application;
using Erp.Identity.Infrastructure.Storage;

namespace Erp.Identity.Application.Handlers;

public sealed class OnboardingRequestService(
    IOnboardingRequestStorage storage,
    IUserStorage userStorage) : IOnboardingRequestService
{
    public async Task<IReadOnlyList<OnboardingRequestListItem>> GetAllAsync(Guid? companyId = null, CancellationToken cancellationToken = default)
    {
        var requests = await storage.GetAllAsync(companyId, cancellationToken);
        return [.. requests.Select(Map)];
    }

    public async Task<InviteToCompanyResult> InviteAsync(InviteToCompanyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Email);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Role);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CompanyName);

        if (request.CompanyId == Guid.Empty)
            throw new ArgumentException("CompanyId is required.", nameof(request));

        var email = request.Email.Trim().ToLowerInvariant();

        // An account that already exists needs no invitation: the caller only has to associate it.
        if (await userStorage.FindByEmailAsync(email, cancellationToken) is { } existing)
        {
            // About to belong to a company, so the account needs the role that makes it a user of
            // one — a sign-up alone leaves it with none.
            await GrantUserRoleAsync(existing.Id, existing.Roles, cancellationToken);
            return new InviteToCompanyResult(existing.Id, null);
        }

        var pending = await storage.GetPendingAsync(email, request.CompanyId, cancellationToken);

        if (pending is null)
        {
            pending = new OnboardingRequest
            {
                Email = email,
                CompanyId = request.CompanyId,
                CompanyName = request.CompanyName.Trim(),
                Role = request.Role.Trim(),
                InvitedByEmail = request.InvitedByEmail?.Trim(),
                Status = Constants.OnboardingStatuses.Pending
            };

            await storage.AddAsync(pending, cancellationToken);
        }
        else
        {
            // Inviting the same person again is a resend, not a second request.
            pending.CompanyName = request.CompanyName.Trim();
            pending.Role = request.Role.Trim();
            pending.InvitedByEmail = request.InvitedByEmail?.Trim();
            pending.LastSentAtUtc = DateTime.UtcNow;

            await storage.UpdateAsync(pending, cancellationToken);
        }

        return new InviteToCompanyResult(null, Map(pending));
    }

    public async Task<IReadOnlyList<OnboardingRequestListItem>> GetPendingForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return [];

        var user = await userStorage.GetUserAsync(userId, cancellationToken);

        if (user is null || !user.IsActive || !user.EmailConfirmed)
            return [];

        var requests = await storage.GetPendingByEmailAsync(user.Email.Trim().ToLowerInvariant(), cancellationToken);
        return [.. requests.Select(Map)];
    }

    public async Task<bool> CompleteAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var request = await storage.GetByIdAsync(id, cancellationToken);

        if (request is null || request.Status != Constants.OnboardingStatuses.Pending)
            return false;

        request.Status = Constants.OnboardingStatuses.Completed;
        request.CompletedAtUtc = DateTime.UtcNow;
        request.CompletedUserId = userId;

        await storage.UpdateAsync(request, cancellationToken);

        // The company now has them as a member: give the account the role that makes it a user of one.
        if (await userStorage.GetUserAsync(userId, cancellationToken) is { } user)
            await GrantUserRoleAsync(user.Id, user.Roles, cancellationToken);

        return true;
    }

    public async Task<bool> CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var request = await storage.GetByIdAsync(id, cancellationToken);

        if (request is null || request.Status != Constants.OnboardingStatuses.Pending)
            return false;

        request.Status = Constants.OnboardingStatuses.Cancelled;
        await storage.UpdateAsync(request, cancellationToken);
        return true;
    }

    /// <summary>Union, not replace: whatever else the account already holds stays untouched.</summary>
    private async Task GrantUserRoleAsync(string userId, IReadOnlyList<string> roles, CancellationToken cancellationToken)
    {
        if (roles.Contains(Constants.Roles.User, StringComparer.OrdinalIgnoreCase))
            return;

        await userStorage.UpdateUserRolesAsync(userId, [.. roles, Constants.Roles.User], cancellationToken);
    }

    private static OnboardingRequestListItem Map(OnboardingRequest request) => new(
        request.Id,
        request.Email,
        request.CompanyId,
        request.CompanyName,
        request.Role,
        request.InvitedByEmail,
        request.Status,
        request.CreatedAtUtc,
        request.LastSentAtUtc,
        request.CompletedAtUtc);
}
