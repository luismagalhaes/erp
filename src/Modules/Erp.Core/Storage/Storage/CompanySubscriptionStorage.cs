using Erp.Core.Domain;
using Erp.Core.Infrastructure.Contracts;
using Erp.Core.Infrastructure.Storage;
using Erp.Storage;
using Microsoft.EntityFrameworkCore;

namespace Erp.Core.Storage.Storage;

public sealed class CompanySubscriptionStorage(AppDbContext dbContext) : ICompanySubscriptionStorage
{
    public Task<CompanySubscription?> GetByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        dbContext.Set<CompanySubscription>()
            .Include(x => x.Plan)
            .FirstOrDefaultAsync(x => x.CompanyId == companyId, cancellationToken);

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

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
