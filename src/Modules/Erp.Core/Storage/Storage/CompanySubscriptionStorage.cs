using Erp.Core.Domain;
using Erp.Core.Infrastructure.Contracts;
using Erp.Core.Infrastructure.Storage;
using Erp.Storage;
using Microsoft.EntityFrameworkCore;

namespace Erp.Core.Storage.Storage;

public sealed class CompanySubscriptionStorage(AppDbContext dbContext) : ICompanySubscriptionStorage
{
    public Task<CompanySubscription?> GetActiveByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        return dbContext.Set<CompanySubscription>()
            .Include(x => x.Plan)
            .Where(x => x.CompanyId == companyId && x.StartedAtUtc <= now && x.ExpiresAtUtc > now)
            .OrderByDescending(x => x.ExpiresAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CompanySubscription>> GetAllByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        await dbContext.Set<CompanySubscription>()
            .Include(x => x.Plan)
            .Where(x => x.CompanyId == companyId)
            .OrderBy(x => x.StartedAtUtc)
            .ToListAsync(cancellationToken);

    public IQueryable<CompanySubscriptionDto> Query() =>
        dbContext.Set<CompanySubscription>()
            .AsNoTracking()
            // Member initialization, not a constructor call: EF Core only keeps the mapping between
            // the DTO members and the columns this way, which is what lets the OData $filter and
            // $orderby applied afterwards be translated to SQL.
            .Select(x => new CompanySubscriptionDto
            {
                Id = x.Id,
                CompanyId = x.CompanyId,
                CompanyName = x.Company.Name,
                PlanId = x.PlanId,
                PlanName = x.Plan.Name,
                PlanPrice = x.Plan.Price,
                BillingPeriod = x.Plan.BillingPeriod,
                StartedAtUtc = x.StartedAtUtc,
                ExpiresAtUtc = x.ExpiresAtUtc
            });

    public async Task AddAsync(CompanySubscription subscription, CancellationToken cancellationToken = default) =>
        await dbContext.Set<CompanySubscription>().AddAsync(subscription, cancellationToken);

    public Task<CompanySubscription?> GetByIdAsync(Guid companyId, Guid subscriptionId, CancellationToken cancellationToken = default) =>
        dbContext.Set<CompanySubscription>()
            .FirstOrDefaultAsync(x => x.Id == subscriptionId && x.CompanyId == companyId, cancellationToken);

    public void Remove(CompanySubscription subscription) =>
        dbContext.Set<CompanySubscription>().Remove(subscription);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
