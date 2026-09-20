using Erp.Core.Domain;
using Erp.Core.Infrastructure.Contracts;
using Erp.Core.Infrastructure.Storage;
using Erp.Storage;
using Microsoft.EntityFrameworkCore;

namespace Erp.Core.Storage.Storage;

public sealed class SubscriptionPlanStorage(AppDbContext dbContext) : ISubscriptionPlanStorage
{
    public async Task<IReadOnlyList<SubscriptionPlan>> GetAllAsync(
        bool activeOnly = false,
        CancellationToken cancellationToken = default) =>
        await dbContext.Set<SubscriptionPlan>()
            .AsNoTracking()
            .Where(x => !activeOnly || x.IsActive)
            .OrderBy(x => x.Price)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public IQueryable<SubscriptionPlanDto> Query() =>
        dbContext.Set<SubscriptionPlan>()
            .AsNoTracking()
            // Member initialization, not a constructor call: EF Core only keeps the mapping between
            // the DTO members and the columns this way, which is what lets the OData $filter and
            // $orderby applied afterwards be translated to SQL.
            .Select(x => new SubscriptionPlanDto
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                Price = x.Price,
                BillingPeriod = x.BillingPeriod,
                TrialDays = x.TrialDays,
                IsActive = x.IsActive
            });

    public Task<SubscriptionPlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Set<SubscriptionPlan>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default) =>
        dbContext.Set<SubscriptionPlan>()
            .AnyAsync(x => x.Name == name && (excludeId == null || x.Id != excludeId), cancellationToken);

    public async Task AddAsync(SubscriptionPlan plan, CancellationToken cancellationToken = default) =>
        await dbContext.Set<SubscriptionPlan>().AddAsync(plan, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
