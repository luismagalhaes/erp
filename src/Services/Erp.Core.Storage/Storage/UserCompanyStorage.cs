using Erp.Core.Domain;
using Erp.Core.Infrastructure.Storage;
using Erp.Core.Storage.Data;
using Microsoft.EntityFrameworkCore;

namespace Erp.Core.Storage.Storage;

public sealed class UserCompanyStorage(CoreDbContext dbContext) : IUserCompanyStorage
{
    public async Task<IReadOnlyList<UserCompany>> GetActiveByUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.UserCompanies
            .AsNoTracking()
            .Include(x => x.Company)
            .Where(x => x.UserId == userId && x.IsActive && x.Company.IsActive)
            .OrderBy(x => x.Company.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<UserCompany?> GetActiveAsync(string userId, Guid companyId, CancellationToken cancellationToken = default)
    {
        return await dbContext.UserCompanies
            .AsNoTracking()
            .Include(x => x.Company)
            .FirstOrDefaultAsync(
                x => x.UserId == userId && x.CompanyId == companyId && x.IsActive && x.Company.IsActive,
                cancellationToken);
    }

    public async Task<IReadOnlyList<UserCompany>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.UserCompanies
            .AsNoTracking()
            .Include(x => x.Company)
            .OrderBy(x => x.UserId)
            .ThenBy(x => x.Company.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<UserCompany?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.UserCompanies
            .Include(x => x.Company)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<bool> ExistsAsync(string userId, Guid companyId, CancellationToken cancellationToken = default)
    {
        return dbContext.UserCompanies.AnyAsync(x => x.UserId == userId && x.CompanyId == companyId, cancellationToken);
    }

    public Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        return dbContext.Companies.AnyAsync(x => x.Id == companyId, cancellationToken);
    }

    public async Task AddAsync(UserCompany userCompany, CancellationToken cancellationToken = default)
    {
        await dbContext.UserCompanies.AddAsync(userCompany, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    public void Remove(UserCompany userCompany)
    {
        dbContext.UserCompanies.Remove(userCompany);
    }
}
