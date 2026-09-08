using Erp.Core.Domain;
using Erp.Core.Infrastructure.Storage;
using Erp.Core.Storage.Data;
using Microsoft.EntityFrameworkCore;
using Erp.Storage;

namespace Erp.Core.Storage.Storage;

public sealed class CompanyStorage(AppDbContext dbContext) : ICompanyStorage
{
    public async Task<IReadOnlyList<Company>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<Company>()
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<Company>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<bool> TaxIdExistsAsync(string taxId, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        return dbContext.Set<Company>()
            .AnyAsync(x => x.TaxId == taxId && (excludeId == null || x.Id != excludeId), cancellationToken);
    }

    public async Task AddAsync(Company company, CancellationToken cancellationToken = default)
    {
        await dbContext.Set<Company>().AddAsync(company, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
