using Erp.Common;
using Erp.Core.Domain;
using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Erp.Core.Infrastructure.Storage;

namespace Erp.Core.Application.Services;

/// <summary>
/// Sign-up for a user who has no company yet. Everywhere else, creating a company and handing
/// someone a membership are two separate SuperAdmin actions; here they are one step the user takes
/// for themselves, which is why it lives behind its own service rather than widening the admin one.
/// </summary>
public sealed class OnboardingService(
    ICompanyAdminService companyAdminService,
    ICompanySubscriptionService subscriptionService,
    IUserCompanyStorage userCompanyStorage,
    IUnitOfWork unitOfWork) : IOnboardingService
{
    /// <summary>The company role the person who signs up gets: it is their own company.</summary>
    private const string OwnerRole = "Owner";

    public async Task<bool> HasCompanyAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var memberships = await userCompanyStorage.GetActiveByUserAsync(userId, cancellationToken);
        return memberships.Count > 0;
    }

    public async Task<CompanyDetailDto> CreateCompanyForUserAsync(
        string userId,
        SelfServiceCompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(request);

        // Onboarding runs once. Without this, anyone could keep creating companies for themselves
        // through a path that deliberately does not require an admin.
        if (await HasCompanyAsync(userId, cancellationToken))
            throw new InvalidOperationException("This account already belongs to a company.");

        // The company, its subscription and the membership are worth nothing apart: a company
        // nobody is a member of is unreachable, and a membership without a subscription would let
        // the sign-up page be skipped entirely.
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        CompanyDetailDto? company = null;

        // The tenant write guard would otherwise refuse every row below: the caller does not belong
        // to this company yet, because the membership that would prove it is itself one of the rows
        // being written here. See RunUnrestrictedAsync's own doc comment for why this is the one
        // legitimate case for it, rather than a reason to widen what the guard allows for everyone.
        await unitOfWork.RunUnrestrictedAsync(async () =>
        {
            company = await companyAdminService.CreateAsync(
                new CreateCompanyRequest(
                    request.Name,
                    request.TaxId,
                    request.LegalName,
                    request.Email,
                    request.Phone,
                    request.Address,
                    request.City,
                    request.PostalCode,
                    request.Country),
                cancellationToken);

            await subscriptionService.AssignAsync(
                company.Id,
                new AssignSubscriptionRequest(request.PlanId),
                cancellationToken);

            await userCompanyStorage.AddAsync(new UserCompany
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CompanyId = company.Id,
                Role = OwnerRole,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            }, cancellationToken);

            await userCompanyStorage.SaveChangesAsync(cancellationToken);
        });

        await transaction.CommitAsync(cancellationToken);

        return company!;
    }
}
