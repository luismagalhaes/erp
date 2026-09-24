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
/// Not unique per company: a company can have several rows over time (expired, currently
/// running, and already scheduled to start later). This carries a CompanyId, so AppDbContext's
/// tenant filter covers it on its own: a company only ever reads its own subscriptions, while the
/// SuperAdmin backoffice — which runs unrestricted — sees them all.
/// </summary>
public interface ICompanySubscriptionStorage
{
    /// <summary>The one currently in effect: StartedAtUtc has passed and ExpiresAtUtc has not, picking the furthest-reaching row if more than one overlaps.</summary>
    Task<CompanySubscription?> GetActiveByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>Every subscription a company has ever had or is scheduled for, oldest first.</summary>
    Task<IReadOnlyList<CompanySubscription>> GetAllByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>The backoffice listing, flattened with the company and plan names.</summary>
    IQueryable<CompanySubscriptionDto> Query();

    Task AddAsync(CompanySubscription subscription, CancellationToken cancellationToken = default);

    /// <summary>Fetches a single row by id, scoped to its company, for the delete flow below.</summary>
    Task<CompanySubscription?> GetByIdAsync(Guid companyId, Guid subscriptionId, CancellationToken cancellationToken = default);

    void Remove(CompanySubscription subscription);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
