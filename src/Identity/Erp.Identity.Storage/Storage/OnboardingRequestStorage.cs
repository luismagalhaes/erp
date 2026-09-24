using Erp.Identity.Common.Constants;
using Erp.Identity.Data;
using Erp.Identity.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace Erp.Identity.Storage.Storage;

public sealed class OnboardingRequestStorage(IDbContextFactory<ApplicationDbContext> dbContextFactory) : IOnboardingRequestStorage
{
    public async Task<IReadOnlyList<OnboardingRequest>> GetAllAsync(Guid? companyId = null, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var query = dbContext.OnboardingRequests.AsNoTracking();

        if (companyId is not null)
            query = query.Where(x => x.CompanyId == companyId);

        return await query.OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OnboardingRequest>> GetPendingByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.OnboardingRequests
            .AsNoTracking()
            .Where(x => x.Email == email && x.Status == Constants.OnboardingStatuses.Pending)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<OnboardingRequest?> GetPendingAsync(string email, Guid companyId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.OnboardingRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Email == email && x.CompanyId == companyId && x.Status == Constants.OnboardingStatuses.Pending,
                cancellationToken);
    }

    public async Task<OnboardingRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.OnboardingRequests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task AddAsync(OnboardingRequest request, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.OnboardingRequests.Add(request);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(OnboardingRequest request, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.OnboardingRequests.Update(request);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
