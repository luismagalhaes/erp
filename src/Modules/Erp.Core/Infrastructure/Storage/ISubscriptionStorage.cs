using Erp.Core.Domain;
using Erp.Core.Infrastructure.Contracts;

namespace Erp.Core.Infrastructure.Storage;

/// <summary>Not company scoped — the packages are the same for everyone, like <see cref="IVatRateStorage"/>.</summary>
public interface ISubscriptionPlanStorage
{
    Task<IReadOnlyList<SubscriptionPlan>> GetAllAsync(bool activeOnly = false, CancellationToken cancellationToken = default);

    /// <summary>The listing left unmaterialised so the grid OData options run in the database.</summary>
    IQueryable<SubscriptionPlanDto> Query();

    Task<SubscriptionPlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task AddAsync(SubscriptionPlan plan, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// One row per company, so this is keyed by company rather than listed per tenant. It carries a
/// CompanyId, so AppDbContext's tenant filter covers it on its own: a company only ever reads its
/// own subscription, while the SuperAdmin backoffice — which runs unrestricted — sees them all.
/// </summary>
public interface ICompanySubscriptionStorage
{
    Task<CompanySubscription?> GetByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>The backoffice listing, flattened with the company and plan names.</summary>
    IQueryable<CompanySubscriptionDto> Query();

    Task AddAsync(CompanySubscription subscription, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
