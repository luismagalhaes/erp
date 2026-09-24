using Erp.Core.Infrastructure.Contracts;

namespace Erp.Core.Infrastructure.Application;

/// <summary>The subscription packages themselves — managed by a SuperAdmin, read by everyone.</summary>
public interface ISubscriptionPlanService
{
    /// <param name="activeOnly">True for the sign-up page, which must not offer a retired package.</param>
    Task<IReadOnlyList<SubscriptionPlanDto>> GetAllAsync(bool activeOnly = false, CancellationToken cancellationToken = default);

    /// <summary>The listing as a queryable, so the grid endpoint can push its OData options down.</summary>
    IQueryable<SubscriptionPlanDto> Query();

    Task<SubscriptionPlanDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SubscriptionPlanDto> CreateAsync(CreateSubscriptionPlanRequest request, CancellationToken cancellationToken = default);

    Task<SubscriptionPlanDto?> UpdateAsync(Guid id, UpdateSubscriptionPlanRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Which company is on which package, and until when.</summary>
public interface ICompanySubscriptionService
{
    /// <summary>The one currently in effect, or null when none of the company's rows covers today.</summary>
    Task<CompanySubscriptionDto?> GetForCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>Every subscription the company has ever had or is scheduled for, oldest first.</summary>
    Task<IReadOnlyList<CompanySubscriptionDto>> GetAllForCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>The listing as a queryable, so the grid endpoint can push its OData options down.</summary>
    IQueryable<CompanySubscriptionDto> Query();

    /// <summary>
    /// Adds a new subscription for the company — it never overwrites an existing row, so a company
    /// can be on a free plan today and already have an annual one scheduled to start later. Used
    /// both by the sign-up page and by a SuperAdmin recording that a company bought a package.
    /// </summary>
    Task<CompanySubscriptionDto> AssignAsync(Guid companyId, AssignSubscriptionRequest request, CancellationToken cancellationToken = default);

    /// <summary>Removes one of the company's subscription rows. Returns false when it does not exist.</summary>
    Task<bool> RemoveAsync(Guid companyId, Guid subscriptionId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The one path by which a user with no company creates their own, without an admin: the package
/// they picked, the company's data, and the membership that ties the two together.
/// </summary>
public interface IOnboardingService
{
    /// <summary>True when the user already belongs to a company, in which case there is nothing to onboard.</summary>
    Task<bool> HasCompanyAsync(string userId, CancellationToken cancellationToken = default);

    Task<CompanyDetailDto> CreateCompanyForUserAsync(
        string userId,
        SelfServiceCompanyRequest request,
        CancellationToken cancellationToken = default);
}
