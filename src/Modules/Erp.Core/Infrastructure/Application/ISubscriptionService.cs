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
    Task<CompanySubscriptionDto?> GetForCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>The listing as a queryable, so the grid endpoint can push its OData options down.</summary>
    IQueryable<CompanySubscriptionDto> Query();

    /// <summary>
    /// Moves a company onto a plan, replacing whatever it was on. Used both by the sign-up page and
    /// by a SuperAdmin recording that a company bought a package.
    /// </summary>
    Task<CompanySubscriptionDto> AssignAsync(Guid companyId, AssignSubscriptionRequest request, CancellationToken cancellationToken = default);
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
